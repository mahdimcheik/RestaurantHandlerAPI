using RestaurantHandler.Models;
using RestaurantHandler.Models.Filters;
using RestaurantHandler.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantHandler.Controllers;

/// <summary>
/// Contrôleur CRUD pour la gestion des utilisateurs (réservé aux administrateurs).
/// </summary>
[Produces("application/json")]
[Consumes("application/json")]
[Route("[controller]")]
[ApiController]
[EnableCors]
[Authorize(Roles = "Manager")]
public class UsersController : ControllerBase
{
    private readonly UserAdministrationService _usersService;

    public UsersController(UserAdministrationService usersService)
    {
        _usersService = usersService;
    }

    /// <summary>
    /// Liste paginée des utilisateurs avec filtres (rôle, nom).
    /// </summary>
    [HttpPost("filter")]
    public async Task<ActionResult<Response<List<UserAdminDetails>>>> GetUsers(
        [FromBody] FilterUsers filters
    )
    {
        var response = await _usersService.GetUsersAsync(filters);
        return StatusCode(response.Status, response);
    }

    /// <summary>
    /// Récupère un utilisateur par son identifiant.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Response<UserDetails>>> GetUserById([FromRoute] Guid id)
    {
        var response = await _usersService.GetUserByIdAsync(id);
        return StatusCode(response.Status, response);
    }

    /// <summary>
    /// Crée un nouvel utilisateur.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Response<UserDetails>>> CreateUser([FromBody] UserCreate user)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(
                new Response<object>
                {
                    Status = 400,
                    Message = "Données de validation invalides",
                    Data = ModelState,
                }
            );
        }

        var response = await _usersService.CreateUserAsync(user);
        return StatusCode(response.Status, response);
    }

    /// <summary>
    /// Met à jour un utilisateur existant.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Response<UserDetails>>> UpdateUser(
        [FromRoute] Guid id,
        [FromBody] UserUpdate user
    )
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(
                new Response<object>
                {
                    Status = 400,
                    Message = "Données de validation invalides",
                    Data = ModelState,
                }
            );
        }

        var response = await _usersService.UpdateUserAsync(id, user);
        return StatusCode(response.Status, response);
    }
}
