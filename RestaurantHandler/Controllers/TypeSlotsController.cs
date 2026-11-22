using Microsoft.AspNetCore.Authorization;
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
public class TypeSlotsController(TypeSlotsService typeSlotsService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("all")]
    [ProducesResponseType(typeof(Response<List<TypeSlotDetails>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Response<List<TypeSlotDetails>>>> GetAllTypeSlots()
    {
        var response = await typeSlotsService.GetAllTypeSlotsAsync();

        if (response.Status == 200)
        {
            return Ok(response);
        }

        return StatusCode(response.Status, response);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Response<TypeSlotDetails>>> GetTypeSlotById(
        [FromRoute] Guid id)
    {
        var response = await typeSlotsService.GetTypeSlotByIdAsync(id);

        return StatusCode(response.Status, response);
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpPost]
    public async Task<ActionResult<Response<TypeSlotDetails>>> CreateTypeSlot(
        [FromBody] TypeSlotCreate typeSlotDto)
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

        var response = await typeSlotsService.CreateTypeSlotAsync(typeSlotDto);

        return StatusCode(response.Status, response);
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpPut]
    public async Task<ActionResult<Response<TypeSlotDetails>>> UpdateTypeSlot(
        [FromBody] TypeSlotUpdate typeSlotDto)
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

        var response = await typeSlotsService.UpdateTypeSlotAsync( typeSlotDto);

        return StatusCode(response.Status, response);
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<Response<bool>>> DeleteTypeSlot(
        [FromRoute] Guid id)
    {
        var response = await typeSlotsService.DeleteTypeSlotAsync(id);

        return StatusCode(response.Status, response);
    }
}
