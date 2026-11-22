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

/// <summary>
/// Point d'entrée unique d'une notification : ligne en base (cloche), événement SignalR
/// <c>Notification</c> (rafraîchissement sans rechargement) et notification native (Web Push).
/// </summary>
public class NotificationDispatcher(
    MainContext context,
    RealtimeService realtime,
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
            await realtime.SendToUserAsync(
                request.UserId,
                nameof(SignalRNotificationTypeEnum.Notification),
                new { Type = request.Type.ToString(), request.Message }
            );

            push.EnqueueNotification(notification.Id);
        }
    }
}
