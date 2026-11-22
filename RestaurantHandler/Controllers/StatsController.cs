using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using RestaurantHandler.Models;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;
using static RestaurantHandler.Controllers.ApiResponses;

namespace RestaurantHandler.Controllers;

/// <summary>
/// Indicateurs de pilotage (E09), jours en heure de Paris. Une commande est comptée si elle est
/// retirée, ou prête et payée, à son heure de retrait.
/// </summary>
[ApiController]
[Route("manage/stats")]
[Produces("application/json")]
[EnableCors]
[Authorize(Roles = nameof(RoleEnum.Manager))]
public class StatsController(StatsService stats) : ControllerBase
{
    /// <summary>Tableau de bord d'un jour (aujourd'hui par défaut) — US-E09-1.</summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<Response<DashboardStats>>> Dashboard([FromQuery] DateOnly? date) =>
        Success(await stats.DashboardAsync(date));

    /// <summary>Ventes par produit, menu et supplément — US-E09-2.</summary>
    [HttpGet("products")]
    public async Task<ActionResult<Response<ProductSalesReport>>> Products([FromQuery] StatsPeriodQuery query) =>
        Success(await stats.ProductsAsync(query));

    /// <summary>Commandes, CA et temps de préparation par jour de semaine et tranche horaire — US-E09-3.</summary>
    [HttpGet("peak-hours")]
    public async Task<ActionResult<Response<PeakHoursReport>>> PeakHours([FromQuery] PeakHoursQuery query) =>
        Success(await stats.PeakHoursAsync(query));
}
