using RestaurantHandler.Services;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantHandler.Infrastructure.Maintenance;

/// <summary>
/// Mode maintenance : 503 sur toute l'API (webhooks Stripe et hub SignalR compris : Stripe
/// rejoue ses webhooks) sauf l'accès minimal qui permet au SuperAdmin de se connecter et de
/// désactiver la maintenance. Les autres comptes ne peuvent pas se connecter
/// (<see cref="SignInService.Login"/>) ; une session déjà ouverte se renouvelle mais n'ouvre
/// plus rien d'autre.
/// </summary>
public sealed class MaintenanceMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Maintenance";

    private static readonly (string Method, string Path)[] AllowedRoutes =
    [
        (HttpMethods.Get, "/maintenance"),
        (HttpMethods.Put, "/maintenance"),
        (HttpMethods.Get, "/version"),
        (HttpMethods.Post, "/auth/login"),
        (HttpMethods.Get, "/auth/refresh-token"),
        (HttpMethods.Get, "/auth/logout"),
        (HttpMethods.Get, "/auth/my-informations"),
    ];

    public async Task InvokeAsync(
        HttpContext context,
        MaintenanceService maintenanceService,
        IProblemDetailsService problemDetailsService
    )
    {
        if (HttpMethods.IsOptions(context.Request.Method) || IsAllowed(context.Request))
        {
            await next(context);
            return;
        }

        var status = await maintenanceService.GetStatusAsync(context.RequestAborted);
        if (!status.Enabled)
        {
            await next(context);
            return;
        }

        var message = MaintenanceService.DisplayMessage(status);
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = message,
            Detail = message,
        };
        problem.Extensions["message"] = message;
        problem.Extensions["maintenance"] = true;

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers[HeaderName] = "1";
        context.Response.Headers.RetryAfter = "300";
        await problemDetailsService.WriteAsync(
            new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem }
        );
    }

    private static bool IsAllowed(HttpRequest request)
    {
        var path = request.Path.Value?.TrimEnd('/') ?? string.Empty;
        foreach (var (method, allowedPath) in AllowedRoutes)
        {
            if (
                HttpMethods.Equals(request.Method, method)
                && string.Equals(path, allowedPath, StringComparison.OrdinalIgnoreCase)
            )
            {
                return true;
            }
        }

        return false;
    }
}
