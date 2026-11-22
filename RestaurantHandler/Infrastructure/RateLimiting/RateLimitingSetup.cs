using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Threading.RateLimiting;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace RestaurantHandler.Infrastructure.RateLimiting;

/// <summary>
/// Limite globale par adresse IP et par utilisateur connecté, plus une limite stricte par IP sur
/// les routes d'authentification (<see cref="AuthPolicy"/>). L'adresse est celle du visiteur tant
/// que <c>UseForwardedHeaders</c> fait confiance au proxy inverse (Program.cs).
/// Ne sont jamais limités : les adresses privées et de bouclage (appels internes, ou proxy qui ne
/// transmet pas X-Forwarded-For), <see cref="EnvironmentVariables.RATE_LIMIT_EXEMPT_IPS"/>, et les
/// routes qui ont leur propre rythme (hub SignalR, Hangfire).
/// </summary>
public static class RateLimitingSetup
{
    public const string AuthPolicy = "auth";

    /// <summary>Validation de commande : par utilisateur, contre les rafales de création.</summary>
    public const string OrderPolicy = "order";
    public const int OrderPerMinute = 10;

    private static readonly string[] ExemptPathPrefixes =
    [
        "/hubs",
        "/hangfire",
        "/version",
    ];

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        var enabled = EnvironmentVariables.RATE_LIMITING_ENABLED;
        var ipPerMinute = EnvironmentVariables.RATE_LIMIT_IP_PER_MINUTE;
        var userPerMinute = EnvironmentVariables.RATE_LIMIT_USER_PER_MINUTE;
        var authPerMinute = EnvironmentVariables.RATE_LIMIT_AUTH_PER_MINUTE;
        var exemptIps = ParseIps(EnvironmentVariables.RATE_LIMIT_EXEMPT_IPS);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejectionAsync;

            if (!enabled)
            {
                options.AddPolicy(AuthPolicy, _ => RateLimitPartition.GetNoLimiter("disabled"));
                options.AddPolicy(OrderPolicy, _ => RateLimitPartition.GetNoLimiter("disabled"));
                return;
            }

            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    var ip = LimitedIp(context, exemptIps);
                    return ip is null || IsExemptPath(context.Request)
                        ? RateLimitPartition.GetNoLimiter("exempt")
                        : SlidingWindow($"ip:{ip}", ipPerMinute);
                }),
                PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    var user = UserKey(context.User);
                    return user is null
                        || LimitedIp(context, exemptIps) is null
                        || IsExemptPath(context.Request)
                        ? RateLimitPartition.GetNoLimiter("exempt")
                        : SlidingWindow($"user:{user}", userPerMinute);
                })
            );

            options.AddPolicy(
                OrderPolicy,
                context => UserKey(context.User) is { } user
                    ? RateLimitPartition.GetFixedWindowLimiter(
                        $"order:{user}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = OrderPerMinute,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                        }
                    )
                    : RateLimitPartition.GetNoLimiter("anonymous")
            );

            options.AddPolicy(
                AuthPolicy,
                context =>
                {
                    var ip = LimitedIp(context, exemptIps);
                    return ip is null
                        ? RateLimitPartition.GetNoLimiter("exempt")
                        : RateLimitPartition.GetFixedWindowLimiter(
                            $"auth:{ip}",
                            _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = authPerMinute,
                                Window = TimeSpan.FromMinutes(1),
                                QueueLimit = 0,
                            }
                        );
                }
            );
        });

        return services;
    }

    private static RateLimitPartition<string> SlidingWindow(string key, int perMinute) =>
        RateLimitPartition.GetSlidingWindowLimiter(
            key,
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = perMinute,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0,
            }
        );

    /// <summary>L'adresse à limiter, ou null si elle est exemptée.</summary>
    private static string? LimitedIp(HttpContext context, HashSet<IPAddress> exemptIps)
    {
        var ip = context.Connection.RemoteIpAddress;
        if (ip is null)
        {
            return null;
        }
        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }
        return IsInternal(ip) || exemptIps.Contains(ip) ? null : ip.ToString();
    }

    public static bool IsInternal(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || (ip.GetAddressBytes()[0] & 0xFE) == 0xFC;
        }
        var b = ip.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168);
    }

    private static string? UserKey(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
            ? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(ClaimTypes.Email)
            : null;

    private static bool IsExemptPath(HttpRequest request) =>
        HttpMethods.IsOptions(request.Method)
        || ExemptPathPrefixes.Any(prefix =>
            request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)
        );

    private static HashSet<IPAddress> ParseIps(string value) =>
        value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => IPAddress.TryParse(entry, out var ip) ? ip : null)
            .OfType<IPAddress>()
            .ToHashSet();

    private static async ValueTask WriteRejectionAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken
    )
    {
        var http = context.HttpContext;
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var delay)
            ? (int)Math.Ceiling(delay.TotalSeconds)
            : 60;
        const string message = "Trop de requêtes. Merci de patienter quelques instants avant de réessayer.";

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = message,
            Detail = message,
        };
        problem.Extensions["message"] = message;
        problem.Extensions["retryAfter"] = retryAfter;

        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        http.Response.Headers.RetryAfter = retryAfter.ToString();

        var problemDetails = http.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetails.WriteAsync(
            new ProblemDetailsContext { HttpContext = http, ProblemDetails = problem }
        );
    }
}
