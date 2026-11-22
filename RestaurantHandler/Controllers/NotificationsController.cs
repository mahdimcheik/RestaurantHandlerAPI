using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using RestaurantHandler.Models;
using RestaurantHandler.Services;

namespace RestaurantHandler.Controllers;

[Produces("application/json")]
[Consumes("application/json")]
[Route("[controller]")]
[ApiController]
[EnableCors]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly NotificationService _notificationService;
    private readonly PushNotificationService _pushService;

    public NotificationsController(NotificationService notificationService, PushNotificationService pushService)
    {
        _notificationService = notificationService;
        _pushService = pushService;
    }

    [HttpPost]
    public async Task<ActionResult<Response<List<NotificationDetails>>>> GetNotifications(
        [FromBody] FilterNotification filter)
    {
        var response = await _notificationService.GetNotificationsByUserAsync(filter, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPut("{id:guid}/toggle-seen")]
    public async Task<ActionResult<Response<NotificationDetails>>> ToggleSeen(
        [FromRoute] Guid id)
    {
        var response = await _notificationService.ToggleSeenAsync(id, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPut("all-toggle-seen")]
    public async Task<ActionResult<Response<NotificationDetails>>> AllToggleSeen(
    )
    {
        var response = await _notificationService.ToggleAllSeenAsync(await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    /// <summary>
    /// Clé publique VAPID à passer à <c>PushManager.subscribe</c>. <c>Data</c> vide : notifications
    /// natives désactivées sur ce serveur.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("push/public-key")]
    public ActionResult<Response<string>> GetPushPublicKey()
    {
        return Ok(
            new Response<string>
            {
                Status = 200,
                Message = "Ok",
                Data = _pushService.IsEnabled ? EnvironmentVariables.VAPID_PUBLIC_KEY : string.Empty,
            }
        );
    }

    /// <summary>Enregistre l'abonnement Web Push de cet appareil pour l'utilisateur connecté.</summary>
    [HttpPost("push/subscribe")]
    public async Task<ActionResult<Response<object>>> SubscribePush([FromBody] PushSubscriptionCreate dto)
    {
        var caller = await this.GetCallerAsync();
        if (caller is null)
            return StatusCode(401, new Response<object> { Status = 401, Message = "Utilisateur non authentifié" });

        await _pushService.SubscribeAsync(caller.UserId, dto, Request.Headers.UserAgent.ToString());
        return Ok(new Response<object> { Status = 200, Message = "Abonnement enregistré" });
    }

    /// <summary>Retire l'abonnement de cet appareil (désactivation ou déconnexion).</summary>
    [HttpPost("push/unsubscribe")]
    public async Task<ActionResult<Response<object>>> UnsubscribePush([FromBody] PushSubscriptionDelete dto)
    {
        var caller = await this.GetCallerAsync();
        if (caller is null)
            return StatusCode(401, new Response<object> { Status = 401, Message = "Utilisateur non authentifié" });

        await _pushService.UnsubscribeAsync(caller.UserId, dto.Endpoint);
        return Ok(new Response<object> { Status = 200, Message = "Abonnement supprimé" });
    }
}
