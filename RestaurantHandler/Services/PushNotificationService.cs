using System.Text.Json;
using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

/// <summary>Contenu d'une notification native, construit au moment de l'envoi.</summary>
public sealed record PushMessage(string Title, string Body, string? Url = null, string? Tag = null);

/// <summary>
/// Notifications natives (Web Push) : gestion des abonnements par appareil et envoi en tâche
/// de fond, pour que l'action de l'utilisateur ne paie jamais l'aller-retour vers les services
/// de push (FCM, Mozilla, Apple). Seuls des identifiants passent par la file : le texte est relu
/// en base à l'envoi, il n'est jamais stocké en clair dans les arguments des tâches.
/// </summary>
public class PushNotificationService(
    MainContext context,
    IWebPushSender sender,
    IBackgroundJobClient jobs,
    ILogger<PushNotificationService> logger
)
{
    private const int MaxBodyLength = 160;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public bool IsEnabled => sender.IsConfigured;

    public async Task SubscribeAsync(Guid userId, PushSubscriptionCreate dto, string? userAgent)
    {
        var existing = await context.WebPushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == dto.Endpoint);
        if (existing is null)
        {
            context.WebPushSubscriptions.Add(
                new WebPushSubscription
                {
                    UserId = userId,
                    Endpoint = dto.Endpoint,
                    P256dh = dto.Keys.P256dh,
                    Auth = dto.Keys.Auth,
                    UserAgent = Truncate(userAgent, 512),
                }
            );
        }
        else
        {
            // Même navigateur, éventuellement repris par un autre compte : le dernier connecté
            // est le seul destinataire.
            existing.UserId = userId;
            existing.P256dh = dto.Keys.P256dh;
            existing.Auth = dto.Keys.Auth;
            existing.UserAgent = Truncate(userAgent, 512);
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync();
    }

    public Task UnsubscribeAsync(Guid userId, string endpoint) =>
        context.WebPushSubscriptions
            .Where(s => s.UserId == userId && s.Endpoint == endpoint)
            .ExecuteDeleteAsync();

    /// <summary>Met en file la version native d'une notification enregistrée.</summary>
    public void EnqueueNotification(Guid notificationId)
    {
        if (IsEnabled)
            jobs.Enqueue<PushNotificationService>(s => s.DeliverNotificationAsync(notificationId, CancellationToken.None));
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task DeliverNotificationAsync(Guid notificationId, CancellationToken ct)
    {
        var notification = await context
            .Notifications.AsNoTracking()
            .Where(n => n.Id == notificationId)
            .Select(n => new { n.UserId, n.Name, n.Message, n.Link })
            .FirstOrDefaultAsync(ct);
        if (notification is null)
            return;

        await DeliverAsync(
            notification.UserId,
            new PushMessage(TitleFor(notification.Name), notification.Message, notification.Link),
            ct
        );
    }

    /// <summary>
    /// Envoi à tous les appareils de l'utilisateur. Pas de rejeu automatique des tâches : un
    /// échec sur un appareil ne doit pas renvoyer le message à ceux qui l'ont déjà reçu.
    /// </summary>
    public async Task DeliverAsync(Guid userId, PushMessage message, CancellationToken ct)
    {
        message = message with { Body = Truncate(message.Body, MaxBodyLength) ?? "" };
        var subscriptions = await context.WebPushSubscriptions.Where(s => s.UserId == userId).ToListAsync(ct);
        if (subscriptions.Count == 0)
            return;

        var payload = BuildPayload(message);
        var now = DateTimeOffset.UtcNow;
        foreach (var subscription in subscriptions)
        {
            switch (await sender.SendAsync(subscription, payload, ct))
            {
                case WebPushSendResult.Sent:
                    subscription.LastUsedAt = now;
                    break;
                case WebPushSendResult.Gone:
                    logger.LogInformation("Abonnement Web Push expiré supprimé ({SubscriptionId})", subscription.Id);
                    context.WebPushSubscriptions.Remove(subscription);
                    break;
            }
        }

        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Format attendu par le service worker Angular (ngsw) : il affiche <c>notification</c> et,
    /// au clic, ouvre <c>url</c> dans l'onglet de l'application déjà ouvert ou dans un nouveau.
    /// </summary>
    public static string BuildPayload(PushMessage message)
    {
        var url = string.IsNullOrWhiteSpace(message.Url) ? "/dashboard" : message.Url;
        var payload = new
        {
            notification = new
            {
                title = message.Title,
                body = message.Body,
                icon = "/assets/icons/icon-192.png",
                badge = "/assets/icons/icon-192.png",
                tag = message.Tag,
                renotify = message.Tag is not null,
                data = new
                {
                    url,
                    onActionClick = new
                    {
                        @default = new { operation = "navigateLastFocusedOrOpen", url },
                    },
                },
            },
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static string TitleFor(string notificationType) =>
        Enum.TryParse<NotificationTypeEnum>(notificationType, out var type)
            ? type switch
            {
                NotificationTypeEnum.Account => "Compte",
                _ => HardCode.APP_NAME,
            }
            : HardCode.APP_NAME;

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..(max - 1)] + "…";
}
