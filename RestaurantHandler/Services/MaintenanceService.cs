using System.Security.Claims;
using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace RestaurantHandler.Services;

/// <summary>
/// Mode maintenance : seul un Manager garde l'accès, pour pouvoir le désactiver.
/// </summary>
public class MaintenanceService(MainContext context, HybridCache cache)
{
    public const string DefaultMessage =
        "BonProf est en maintenance. Merci de revenir un peu plus tard.";

    public async Task<MaintenanceStatus> GetStatusAsync(CancellationToken ct = default) =>
        await cache.GetOrCreateAsync(
            CacheKeys.MAINTENANCE,
            async token =>
                await context
                    .PlatformSettings.AsNoTracking()
                    .Where(s => s.Id == HardCode.PLATFORM_SETTINGS_ID)
                    .Select(s => new MaintenanceStatus
                    {
                        Enabled = s.MaintenanceEnabled,
                        Message = s.MaintenanceMessage,
                    })
                    .FirstOrDefaultAsync(token) ?? new MaintenanceStatus(),
            CacheKeys.Maintenance,
            cancellationToken: ct
        );

    public async Task<Response<MaintenanceStatus>> UpdateAsync(
        MaintenanceUpdate update,
        CallerIdentity? caller
    )
    {
        if (caller is null)
        {
            return new Response<MaintenanceStatus> { Status = 401, Message = "Non authentifié" };
        }

        var settings = await context.PlatformSettings.FirstAsync(s =>
            s.Id == HardCode.PLATFORM_SETTINGS_ID
        );

        settings.MaintenanceEnabled = update.Enabled;
        settings.MaintenanceMessage = string.IsNullOrWhiteSpace(update.Message)
            ? null
            : update.Message.Trim();
        settings.UpdatedAt = DateTimeOffset.UtcNow;
        settings.UpdatedById = caller.UserId;
        await context.SaveChangesAsync();
        await cache.RemoveAsync(CacheKeys.MAINTENANCE);

        return new Response<MaintenanceStatus>
        {
            Status = 200,
            Message = update.Enabled ? "Maintenance activée" : "Maintenance désactivée",
            Data = new MaintenanceStatus
            {
                Enabled = settings.MaintenanceEnabled,
                Message = settings.MaintenanceMessage,
            },
        };
    }

    /// <summary>Message à afficher : celui du Manager, sinon le texte par défaut.</summary>
    public static string DisplayMessage(MaintenanceStatus status) =>
        status.Message ?? DefaultMessage;
}
