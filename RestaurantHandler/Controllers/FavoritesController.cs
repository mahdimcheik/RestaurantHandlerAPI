using RestaurantHandler.Models;
using RestaurantHandler.Services.Interfaces;
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
[Authorize(Roles = "Student")]
public class FavoritesController(IFavoriteService favoriteService) : ControllerBase
{
    [HttpPost("add")]
    public async Task<ActionResult<Response<FavoriteDetails>>> AddFavorite(
        [FromBody] CreateFavorite dto
    )
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new Response<object>
            {
                Status = 400,
                Message = "Données de validation invalides",
                Data = ModelState
            });
        }

        var response = await favoriteService.AddFavoriteAsync(dto, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpDelete("remove/{teacherId:guid}")]
    public async Task<ActionResult<Response<object>>> RemoveFavorite([FromRoute] Guid teacherId)
    {
        var response = await favoriteService.RemoveFavoriteAsync(teacherId, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpGet("is-favorite/{teacherId:guid}")]
    public async Task<ActionResult<Response<bool>>> IsFavorite([FromRoute] Guid teacherId)
    {
        var response = await favoriteService.IsFavoriteAsync(teacherId, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }
}
