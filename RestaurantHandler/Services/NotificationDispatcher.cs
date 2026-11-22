using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

/// <summary>Une notification à remettre à un utilisateur.</summary>
public sealed record NotificationRequest(
    Guid UserId,
    string? Email,
    NotificationTypeEnum Type,
    string Message,
    string Color,
    string Icon,
    string? Link = null
);

/// <summary>Destinataire d'administration, avec son rôle pour construire ses liens.</summary>
public sealed record AdminRecipient(Guid UserId, string? Email, bool IsSuperAdmin);

/// <summary>
/// Point d'entrée unique d'une notification : ligne en base (cloche), événement SignalR
/// <c>Notification</c> (rafraîchissement sans rechargement) et notification native (Web Push).
/// </summary>
public class NotificationDispatcher(
    MainContext context,
    SignalRNotificationsService signalR,
    PushNotificationService push
)
{
    private readonly List<(NotificationRequest Request, Notification Entity)> _staged = [];

    /// <summary>Persiste, puis publie. À appeler hors transaction ouverte.</summary>
    public async Task NotifyAsync(params NotificationRequest[] requests)
    {
        Stage(requests);
        await context.SaveChangesAsync();
        await PublishAsync();
    }

    /// <summary>
    /// Ajoute les notifications au change tracker sans sauvegarder : l'appelant les enregistre
    /// avec son propre <c>SaveChanges</c>/commit, puis appelle <see cref="PublishAsync"/>.
    /// </summary>
    public void Stage(params NotificationRequest[] requests)
    {
        foreach (var request in requests)
        {
            var notification = new Notification
            {
                Message = request.Message,
                Name = request.Type.ToString(),
                Color = request.Color,
                Icon = request.Icon,
                IsSeen = false,
                UserId = request.UserId,
                Link = request.Link,
            };
            context.Notifications.Add(notification);
            _staged.Add((request, notification));
        }
    }

    /// <summary>Temps réel et natif pour les notifications enregistrées depuis <see cref="Stage"/>.</summary>
    public async Task PublishAsync()
    {
        var staged = _staged.ToList();
        _staged.Clear();

        foreach (var (request, notification) in staged)
        {
            if (!string.IsNullOrEmpty(request.Email))
            {
                await signalR.SendMessageByUserEmail(
                    request.Email,
                    nameof(SignalRNotificationTypeEnum.Notification),
                    new { Type = request.Type.ToString(), request.Message }
                );
            }

            push.EnqueueNotification(notification.Id);
        }
    }

    public async Task<List<AdminRecipient>> GetAdminRecipientsAsync()
    {
        var roles = await context
            .UserRoles.Where(ur => ur.RoleId == HardCode.ROLE_ADMIN || ur.RoleId == HardCode.ROLE_SUPER_ADMIN)
            .Select(ur => new { ur.UserId, ur.RoleId })
            .ToListAsync();
        var superAdminIds = roles.Where(r => r.RoleId == HardCode.ROLE_SUPER_ADMIN).Select(r => r.UserId).ToHashSet();
        var adminIds = roles.Select(r => r.UserId).Distinct().ToList();

        var users = await context
            .Users.Where(u => adminIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToListAsync();
        return users.Select(u => new AdminRecipient(u.Id, u.Email, superAdminIds.Contains(u.Id))).ToList();
    }
}

/// <summary>Chemins front ouverts au clic sur une notification (cf. <c>app.routes.ts</c>).</summary>
public static class NotificationLinks
{
    public static string AdminTeacherDocuments(Guid teacherId, bool isSuperAdmin) =>
        $"{AdminBase(isSuperAdmin)}/teacher/{teacherId}/documents";

    public const string TeacherDocuments = "/dashboard/teacher/documents";

    private static string AdminBase(bool isSuperAdmin) => isSuperAdmin ? "/dashboard/superadmin" : "/dashboard/admin";
}
