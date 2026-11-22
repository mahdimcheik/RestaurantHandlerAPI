using System.Security.Claims;
using RestaurantHandler.Contexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Utilities;

/// <summary>Identité de l'appelant, lue dans la session et transmise aux services.</summary>
public sealed record CallerIdentity(Guid UserId, bool IsManager);

public sealed class CallerResolver(MainContext context)
{
    /// <summary>Null si la session ne désigne aucun compte existant.</summary>
    public async Task<CallerIdentity?> ResolveAsync(ClaimsPrincipal principal)
    {
        var email = principal.FindFirstValue(ClaimTypes.Email);
        if (email is null)
            return null;

        var userId = await context
            .Users.Where(u => u.Email == email)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync();

        return userId is null
            ? null
            : new CallerIdentity(userId.Value, principal.IsInRole(nameof(RoleEnum.Manager)));
    }
}

public static class CallerControllerExtensions
{
    /// <summary>
    /// L'appelant de la requête, null s'il est anonyme ou si son compte n'existe plus : le service
    /// décide de la réponse, comme il le faisait quand il lisait lui-même la session.
    /// </summary>
    public static Task<CallerIdentity?> GetCallerAsync(this ControllerBase controller) =>
        controller
            .HttpContext.RequestServices.GetRequiredService<CallerResolver>()
            .ResolveAsync(controller.User);
}
