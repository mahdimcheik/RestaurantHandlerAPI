using System.Net;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using WebPush;

namespace RestaurantHandler.Services;

public enum WebPushSendResult
{
    Sent,

    /// <summary>L'abonnement n'existe plus chez le service de push (404/410) : à supprimer.</summary>
    Gone,
    Failed,
}

/// <summary>Envoi d'un message chiffré à un abonnement ; remplacé par un faux dans les tests.</summary>
public interface IWebPushSender
{
    bool IsConfigured { get; }

    Task<WebPushSendResult> SendAsync(WebPushSubscription subscription, string payload, CancellationToken ct);
}

public sealed class VapidWebPushSender(ILogger<VapidWebPushSender> logger) : IWebPushSender, IDisposable
{
    // Un message non distribué au bout d'un jour n'a plus d'intérêt.
    private const int TimeToLiveSeconds = 24 * 60 * 60;

    private readonly WebPushClient _client = new();

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(EnvironmentVariables.VAPID_PUBLIC_KEY)
        && !string.IsNullOrWhiteSpace(EnvironmentVariables.VAPID_PRIVATE_KEY);

    public async Task<WebPushSendResult> SendAsync(
        WebPushSubscription subscription,
        string payload,
        CancellationToken ct
    )
    {
        var options = new Dictionary<string, object>
        {
            ["vapidDetails"] = new VapidDetails(
                EnvironmentVariables.VAPID_SUBJECT,
                EnvironmentVariables.VAPID_PUBLIC_KEY,
                EnvironmentVariables.VAPID_PRIVATE_KEY
            ),
            ["TTL"] = TimeToLiveSeconds,
        };

        try
        {
            await _client.SendNotificationAsync(
                new PushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth),
                payload,
                options,
                ct
            );
            return WebPushSendResult.Sent;
        }
        catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
        {
            return WebPushSendResult.Gone;
        }
        catch (Exception ex) when (ex is WebPushException or HttpRequestException or ArgumentException)
        {
            logger.LogWarning(ex, "Échec de l'envoi Web Push à l'abonnement {SubscriptionId}", subscription.Id);
            return WebPushSendResult.Failed;
        }
    }

    public void Dispose() => _client.Dispose();
}
