using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Models;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;
using static RestaurantHandler.Controllers.ApiResponses;

namespace RestaurantHandler.Controllers;

/// <summary>Lecture publique : carte, état d'ouverture, créneaux, informations.</summary>
[ApiController]
[Produces("application/json")]
[EnableCors]
[AllowAnonymous]
public class PublicController(PublicCatalogService catalog, RestaurantService restaurant) : ControllerBase
{
    /// <summary>Carte complète en une requête, avec disponibilité et état d'ouverture.</summary>
    [HttpGet("menu")]
    public async Task<ActionResult<Response<PublicCard>>> GetCard() => Success(await catalog.GetCardAsync());

    [HttpGet("restaurant/status")]
    public async Task<ActionResult<Response<OpeningStateDto>>> GetStatus() => Success(await restaurant.GetOpeningStateAsync());

    /// <summary>Créneaux de retrait du jour (vide pendant une pause).</summary>
    [HttpGet("restaurant/slots")]
    public async Task<ActionResult<Response<List<PickupSlotDto>>>> GetSlots() => Success(await restaurant.GetSlotsAsync());

    [HttpGet("restaurant/info")]
    public async Task<ActionResult<Response<RestaurantInfo>>> GetInfo() => Success(await restaurant.GetInfoAsync());
}

[ApiController]
[Route("manage/restaurant")]
[Produces("application/json")]
[EnableCors]
[Authorize(Roles = "Manager")]
public class RestaurantController(RestaurantService restaurant) : ControllerBase
{
    [HttpGet("hours")]
    public async Task<ActionResult<Response<List<OpeningRangeDto>>>> GetHours() => Success(await restaurant.GetHoursAsync());

    /// <summary>Remplace toute la semaine d'horaires.</summary>
    [HttpPut("hours")]
    public async Task<ActionResult<Response<List<OpeningRangeDto>>>> ReplaceHours([FromBody] WeekHoursUpdate dto) =>
        Success(await restaurant.ReplaceHoursAsync(dto));

    [HttpGet("closures")]
    public async Task<ActionResult<Response<List<ClosureDetails>>>> ListClosures([FromQuery] bool includePast = false) =>
        Success(await restaurant.ListClosuresAsync(includePast));

    [HttpPost("closures")]
    public async Task<ActionResult<Response<ClosureDetails>>> CreateClosure([FromBody] ClosureWrite dto) =>
        CreatedWith(await restaurant.CreateClosureAsync(dto));

    [HttpDelete("closures/{id:guid}")]
    public async Task<ActionResult<Response<object>>> DeleteClosure(Guid id)
    {
        await restaurant.DeleteClosureAsync(id);
        return Done("Fermeture supprimée");
    }

    /// <summary>Suspend les commandes en ligne (15, 30, 60 min ou sans fin).</summary>
    [HttpPost("pause")]
    public async Task<ActionResult<Response<OpeningStateDto>>> Pause([FromBody] PauseRequest dto) =>
        Success(await restaurant.PauseAsync(dto));

    [HttpPost("resume")]
    public async Task<ActionResult<Response<OpeningStateDto>>> Resume() => Success(await restaurant.ResumeAsync());

    [HttpGet("settings")]
    public async Task<ActionResult<Response<RestaurantSettingsDetails>>> GetSettings() =>
        Success(await restaurant.GetSettingsAsync());

    [HttpPut("settings")]
    public async Task<ActionResult<Response<RestaurantSettingsDetails>>> UpdateSettings([FromBody] RestaurantSettingsWrite dto) =>
        Success(await restaurant.UpdateSettingsAsync(dto, await RequireCallerAsync()));

    [HttpPost("settings/logo")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<Response<RestaurantSettingsDetails>>> SetLogo(IFormFile file, CancellationToken ct) =>
        Success(await restaurant.SetLogoAsync(file, await RequireCallerAsync(), ct));

    [HttpGet("settings/history")]
    public async Task<ActionResult<Response<List<RestaurantSettingsHistoryItem>>>> GetSettingsHistory([FromQuery] int take = 50) =>
        Success(await restaurant.GetSettingsHistoryAsync(take));

    private async Task<CallerIdentity> RequireCallerAsync() =>
        await this.GetCallerAsync() ?? throw new BusinessForbiddenException("Compte introuvable.");
}
