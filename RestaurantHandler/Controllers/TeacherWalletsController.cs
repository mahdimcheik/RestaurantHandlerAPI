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
[Authorize(Roles = "Teacher,Admin,SuperAdmin")]
public class TeacherWalletsController(
    TeacherWalletService teacherWalletService,
    CallerResolver callerResolver
) : ControllerBase
{
    private static Response<object> Unauthenticated =>
        new()
        {
            Status = 401,
            Message = "Utilisateur non authentifié",
        };

    /// <summary>
    /// Paginated list of a teacher's wallet transactions / payouts.
    /// Filterable by reservation number and transaction type via <see cref="CustomTableState.Filters"/>.
    /// </summary>
    [HttpPost("teacher/{teacherId:guid}/transactions")]
    public async Task<ActionResult<Response<List<TeacherWalletTransactionDetails>>>> GetTransactionsByTeacher(
        [FromRoute] Guid teacherId,
        [FromBody] CustomTableState query
    )
    {
        var caller = await callerResolver.ResolveAsync(User);
        if (caller is null)
            return Unauthorized(Unauthenticated);

        var response = await teacherWalletService.GetTransactionsByTeacherAsync(
            teacherId,
            query,
            caller
        );
        return StatusCode(response.Status, response);
    }

    /// <summary>
    /// Solde d'un prof : crédits acquis non versés, dette (pénalités d'annulation / clawbacks),
    /// solde net et versements en cours. Un solde négatif signifie que le prof doit de l'argent
    /// à la plateforme — il sera prélevé sur ses prochains versements.
    /// </summary>
    [HttpGet("teacher/{teacherId:guid}/balance")]
    public async Task<ActionResult<Response<TeacherWalletBalance>>> GetBalance(
        [FromRoute] Guid teacherId
    )
    {
        var caller = await callerResolver.ResolveAsync(User);
        if (caller is null)
            return Unauthorized(Unauthenticated);

        var response = await teacherWalletService.GetBalanceAsync(teacherId, caller);
        return StatusCode(response.Status, response);
    }

    /// <summary>
    /// A single wallet transaction with reservation, student and teacher details.
    /// </summary>
    [HttpGet("transactions/{id:guid}")]
    public async Task<ActionResult<Response<TeacherWalletTransactionDetail>>> GetTransactionById(
        [FromRoute] Guid id
    )
    {
        var caller = await callerResolver.ResolveAsync(User);
        if (caller is null)
            return Unauthorized(Unauthenticated);

        var response = await teacherWalletService.GetTransactionByIdAsync(id, caller);
        return StatusCode(response.Status, response);
    }
}
