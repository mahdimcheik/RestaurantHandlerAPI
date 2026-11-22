using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Authorization;
using RestaurantHandler.Models;
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
public class CursusController(CursusService cursusService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("all")]
    public async Task<ActionResult<Response<List<CursusDetails>>>> GetAllCursuses()
    {
        var response = await cursusService.GetAllCursusesAsync();

        if (response.Status == 200)
        {
            return Ok(response);
        }

        return StatusCode(response.Status, response);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Response<CursusDetails>>> GetCursusById(
        [FromRoute] Guid id)
    {
        var response = await cursusService.GetCursusByIdAsync(id);

        return StatusCode(response.Status, response);
    }

    [AllowAnonymous]
    [HttpGet("level/{id:guid}")]
    public async Task<ActionResult<Response<List<CursusDetails>>>> GetCursusesByLevelId(
        [FromRoute] Guid id)
    {
        var response = await cursusService.GetCursusesByLevelIdAsync(id);

        if (response.Status == 200)
        {
            return Ok(response);
        }

        return StatusCode(response.Status, response);
    }

    [AllowAnonymous]
    [HttpGet("teacher/{id:guid}")]
    public async Task<ActionResult<Response<List<CursusDetails>>>> GetCursusesByTeacher(
        [FromRoute] Guid id)
    {
        var response = await cursusService.GetCursusesByTeacherIdAsync(id);

        if (response.Status == 200)
        {
            return Ok(response);
        }

        return StatusCode(response.Status, response);
    }

    [Authorize(Roles = "Teacher")]
    [HttpPost]
    public async Task<ActionResult<Response<CursusDetails>>> CreateCursus(
        [FromBody] CursusCreate cursusDto)
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

        var response = await cursusService.CreateCursusAsync(cursusDto, await this.GetCallerAsync());

        return StatusCode(response.Status, response);
    }

    [Authorize(Roles = "Teacher")]
    [HttpPut]
    public async Task<ActionResult<Response<CursusDetails>>> UpdateCursus(
        [FromBody] CursusUpdate cursusDto)
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

        var response = await cursusService.UpdateCursusAsync(cursusDto, await this.GetCallerAsync());

        return StatusCode(response.Status, response);
    }

    [Authorize(Roles = "Teacher")]
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<Response<object>>> DeleteCursus(
        [FromRoute] Guid id)
    {
        var response = await cursusService.DeleteCursusAsync(id, await this.GetCallerAsync());

        return StatusCode(response.Status, response);
    }
}
