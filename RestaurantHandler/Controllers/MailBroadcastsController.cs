using RestaurantHandler.Models;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantHandler.Controllers;

/// <summary>Envois groupés par e-mail (ADR-0023, US-E17-08 et US-E13-08).</summary>
[Produces("application/json")]
[Consumes("application/json")]
[Route("mail-broadcasts")]
[ApiController]
[EnableCors]
public class MailBroadcastsController(MailBroadcastService broadcastService) : ControllerBase
{
    [HttpPost("admin")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<Response<MailBroadcastSent>>> SendAsAdmin(
        [FromBody] AdminMailBroadcastCreate dto
    )
    {
        var response = await broadcastService.SendAsAdminAsync(dto, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPost("admin/count")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<Response<int>>> CountAdminAudience([FromBody] AdminMailAudienceCount dto)
    {
        var response = await broadcastService.CountAdminAudienceAsync(dto, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    /// <summary>Historique : tout pour l'administration, ses propres envois pour un professeur.</summary>
    [HttpPost("search")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<Response<List<MailBroadcastSummary>>>> Search([FromBody] CustomTableState query)
    {
        var response = await broadcastService.SearchHistoryAsync(query, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }
}
