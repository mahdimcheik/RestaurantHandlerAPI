using RestaurantHandler.Models;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantHandler.Controllers;

/// <summary>Élèves du professeur connecté (US-E20-01), route cible du contrat d'API.</summary>
[Produces("application/json")]
[Consumes("application/json")]
[Route("teachers/me/students")]
[ApiController]
[EnableCors]
[Authorize(Roles = "Teacher")]
public class MyStudentsController(BoardService boardService) : ControllerBase
{
    [HttpPost("search")]
    public async Task<ActionResult<Response<List<MyStudentDetails>>>> SearchMyStudents(
        [FromBody] CustomTableState query
    )
    {
        var response = await boardService.SearchMyStudentsAsync(query, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }
}
