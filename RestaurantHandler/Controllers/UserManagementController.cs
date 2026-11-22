using RestaurantHandler.Models;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantHandler.Controllers;

/// <summary>
/// Contrôleur pour la gestion des utilisateurs côté Manager.
/// </summary>
[Produces("application/json")]
[Consumes("application/json")]
[Route("[controller]")]
[ApiController]
[EnableCors]
[Authorize(Roles = "Manager")]
public class UserManagementController : ControllerBase
{
    private readonly UserManagementService _userManagementService;

    public UserManagementController(UserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    /// <summary>
    /// Modifie manuellement le statut du compte d'un utilisateur (actif, en attente, banni).
    /// </summary>
    [HttpPatch("users/{userId:guid}/status")]
    public async Task<ActionResult<Response<UserDetails>>> SetUserAccountStatus(
        [FromRoute] Guid userId,
        [FromQuery] Guid statusId
    )
    {
        var response = await _userManagementService.SetUserAccountStatusAsync(userId, statusId);
        return StatusCode(response.Status, response);
    }
}
