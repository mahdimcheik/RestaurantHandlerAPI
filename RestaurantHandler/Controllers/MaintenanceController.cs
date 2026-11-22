using RestaurantHandler.Models;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantHandler.Controllers;

[Produces("application/json")]
[Consumes("application/json")]
[Route("[controller]")]
[ApiController]
[EnableCors]
public class MaintenanceController(MaintenanceService maintenanceService) : ControllerBase
{
    /// <summary>État public du mode maintenance, lu par le front au démarrage.</summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<Response<MaintenanceStatus>>> GetMaintenanceStatus()
    {
        var status = await maintenanceService.GetStatusAsync(HttpContext.RequestAborted);

        return Ok(
            new Response<MaintenanceStatus>
            {
                Status = 200,
                Message = "État de la maintenance",
                Data = status,
            }
        );
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpPut]
    public async Task<ActionResult<Response<MaintenanceStatus>>> UpdateMaintenance(
        [FromBody] MaintenanceUpdate update
    )
    {
        var response = await maintenanceService.UpdateAsync(update, await this.GetCallerAsync());

        if (response.Status == 200)
        {
            return Ok(response);
        }

        return StatusCode(response.Status, response);
    }
}
