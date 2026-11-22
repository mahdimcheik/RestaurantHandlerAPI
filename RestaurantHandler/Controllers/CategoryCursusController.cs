using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using RestaurantHandler.Models;
using RestaurantHandler.Services;

namespace RestaurantHandler.Controllers;


/// <summary>
/// Contrôleur pour la gestion des catégories de cursus
/// </summary>
[Produces("application/json")]
[Consumes("application/json")]
[Route("[controller]")]
[ApiController]
[EnableCors]
public class CategoryCursusController(CategoryCursusService categoryCursusService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("all")]
    public async Task<ActionResult<Response<List<CategoryCursusDetails>>>> GetAllCategoryCursus()
    {
        var response = await categoryCursusService.GetAllCategoryCursusAsync(await this.GetCallerAsync());

        if (response.Status == 200)
        {
            return Ok(response);
        }

        return StatusCode(response.Status, response);
    }
    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Response<CategoryCursusDetails>>> GetCategoryCursusById(
        [FromRoute] Guid id)
    {
        var response = await categoryCursusService.GetCategoryCursusByIdAsync(id);

        return StatusCode(response.Status, response);
    }
    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<Response<CategoryCursusDetails>>> CreateCategoryCursus(
        [FromBody] CategoryCursusCreate categoryDto)
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

        var response = await categoryCursusService.CreateCategoryCursusAsync(categoryDto);

        return StatusCode(response.Status, response);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<Response<CategoryCursusDetails>>> UpdateCategoryCursus(
        [FromRoute] Guid id,
        [FromBody] CategoryCursusUpdate categoryDto)
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

        var response = await categoryCursusService.UpdateCategoryCursusAsync(id, categoryDto);

        return StatusCode(response.Status, response);
    }
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<Response<object>>> DeleteCategoryCursus(
        [FromRoute] Guid id)
    {
        var response = await categoryCursusService.DeleteCategoryCursusAsync(id);

        return StatusCode(response.Status, response);
    }

    [HttpPatch("{id:guid}/unarchive")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<Response<CategoryCursusDetails>>> UnarchiveCategoryCursus(
        [FromRoute] Guid id)
    {
        var response = await categoryCursusService.UnarchiveCategoryCursusAsync(id);

        return StatusCode(response.Status, response);
    }
}