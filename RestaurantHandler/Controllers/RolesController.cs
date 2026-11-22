using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using RestaurantHandler.Models;
using RestaurantHandler.Services;

namespace RestaurantHandler.Controllers;

/// <summary>
/// Contrôleur pour la gestion des rôles
/// </summary>
[Produces("application/json")]
[Consumes("application/json")]
[Route("[controller]")]
[ApiController]
[EnableCors]
public class RolesController(RolesService rolesService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("all")]
    public async Task<ActionResult<Response<List<RoleDetails>>>> GetAllRoles()
    {
        var response = await rolesService.GetAllRolesAsync();

        if (response.Status == 200)
        {
            return Ok(response);
        }

        return StatusCode(response.Status, response);
    }
}
