using Hangfire.Dashboard;

namespace RestaurantHandler.Utilities;

/// <summary>
/// Réserve le tableau de bord Hangfire à l'administration : les arguments des tâches y sont
/// affichés en clair. Ne fonctionne que monté après <c>UseAuthentication</c>, sinon
/// <c>HttpContext.User</c> est encore vide, et avant <c>UseAuthorization</c>, sinon la
/// <c>FallbackPolicy</c> refuse la requête avant Hangfire. Avec <paramref name="allowLocalRequests"/>,
/// une requête depuis la machine elle-même passe aussi : un navigateur n'envoie pas le JWT Bearer.
/// </summary>
public sealed class HangfireAdminAuthorizationFilter(bool allowLocalRequests = false)
    : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        if (allowLocalRequests && new LocalRequestsOnlyAuthorizationFilter().Authorize(context))
            return true;

        var user = context.GetHttpContext().User;
        return user.Identity?.IsAuthenticated == true
            && (user.IsInRole(nameof(RoleEnum.Admin)) || user.IsInRole(nameof(RoleEnum.SuperAdmin)));
    }
}
