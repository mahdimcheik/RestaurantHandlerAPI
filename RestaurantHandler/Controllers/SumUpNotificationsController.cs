using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantHandler.Integrations.SumUp;
using RestaurantHandler.Services;

namespace RestaurantHandler.Controllers;

/// <summary>
/// Notifications SumUp (non signées) : elles ne servent qu'à déclencher une relecture du
/// checkout chez SumUp. Réponse 2xx vide ; une erreur de lecture renvoie 502 pour que SumUp
/// réessaie (1 min, 5 min, 20 min, 2 h), et la tâche de réconciliation rattrape le reste.
/// </summary>
[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class SumUpNotificationsController(OnlinePaymentService payments, ILogger<SumUpNotificationsController> logger)
    : ControllerBase
{
    [HttpPost(OnlinePaymentService.NotificationPath)]
    public async Task<IActionResult> Notify([FromBody] JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object
            || !body.TryGetProperty("event_type", out var type) || type.GetString() != "CHECKOUT_STATUS_CHANGED"
            || !body.TryGetProperty("id", out var id) || string.IsNullOrWhiteSpace(id.GetString()))
        {
            return NoContent();
        }

        try
        {
            await payments.HandleNotificationAsync(id.GetString()!);
            return NoContent();
        }
        catch (Exception ex) when (ex is SumUpException or HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "Notification SumUp non traitée pour {CheckoutId}.", id.GetString());
            return StatusCode(StatusCodes.Status502BadGateway);
        }
    }
}
