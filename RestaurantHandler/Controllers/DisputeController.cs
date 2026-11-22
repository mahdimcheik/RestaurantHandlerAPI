using RestaurantHandler.Models;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantHandler.Controllers;

/// <summary>
/// Contrôleur pour la gestion des litiges (Feature 8). L'élève ouvre un litige ; élève, prof
/// et admin consultent (chacun son périmètre) ; l'admin voit tout.
/// </summary>
[Produces("application/json")]
[Consumes("application/json")]
[Route("[controller]")]
[ApiController]
[EnableCors]
public class DisputeController(DisputeService disputeService) : ControllerBase
{
    [Authorize]
    [HttpGet("statuses")]
    [ProducesResponseType(typeof(Response<List<DisputeStatusDetails>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Response<List<DisputeStatusDetails>>>> GetAllDisputeStatuses()
    {
        var response = await disputeService.GetAllDisputeStatusesAsync();
        return StatusCode(response.Status, response);
    }

    [Authorize]
    [HttpGet("outcomes")]
    [ProducesResponseType(typeof(Response<List<DisputeOutcomeDetails>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Response<List<DisputeOutcomeDetails>>>> GetAllDisputeOutcomes()
    {
        var response = await disputeService.GetAllDisputeOutcomesAsync();
        return StatusCode(response.Status, response);
    }

    [Authorize(Roles = "Student")]
    [HttpPost("student/paginated")]
    public async Task<ActionResult<Response<List<DisputeDetails>>>> GetDisputesByStudent(
        [FromBody] CustomTableState query
    )
    {
        var response = await disputeService.GetDisputesByStudentPaginated(query, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [Authorize(Roles = "Teacher")]
    [HttpPost("teacher/paginated")]
    public async Task<ActionResult<Response<List<DisputeDetails>>>> GetDisputesByTeacher(
        [FromBody] CustomTableState query
    )
    {
        var response = await disputeService.GetDisputesByTeacherPaginated(query, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [Authorize(Roles = "Admin, SuperAdmin")]
    [HttpPost("admin/paginated")]
    public async Task<ActionResult<Response<List<DisputeDetails>>>> GetDisputesByAdmin(
        [FromBody] CustomTableState query
    )
    {
        var response = await disputeService.GetDisputesByAdminPaginated(query);
        return StatusCode(response.Status, response);
    }

    [Authorize]
    [HttpGet("{disputeId:guid}")]
    public async Task<ActionResult<Response<DisputeDetails>>> GetDisputeById(
        [FromRoute] Guid disputeId
    )
    {
        var response = await disputeService.GetDisputeById(disputeId, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [Authorize(Roles = "Student")]
    [HttpPost("student/open")]
    public async Task<ActionResult<Response<object>>> OpenDispute([FromBody] DisputeCreate dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }
        await disputeService.OpenDisputeAsync(dto, await this.GetCallerAsync());
        return StatusCode(
            200,
            new Response<object>
            {
                Status = 200,
                Message = "Litige ouvert",
                Data = null,
            }
        );
    }

    /// <summary>
    /// L'admin résout un litige via un unique montant <c>RefundAmount</c> (R, remboursé à
    /// l'élève) : <c>0</c> = payout total au prof (réservation → <c>PAYABLE</c>),
    /// <c>== Amount</c> = remboursement total (→ <c>CANCELLED</c>), entre = partiel
    /// (remboursement + payout du reste). Commission prélevée au prorata sur le prof.
    /// </summary>
    [Authorize(Roles = "Admin, SuperAdmin")]
    [HttpPost("admin/close")]
    public async Task<ActionResult<Response<object>>> CloseDispute([FromBody] DisputeClose dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }
        await disputeService.CloseDisputeAsync(dto, await this.GetCallerAsync());
        return StatusCode(
            200,
            new Response<object>
            {
                Status = 200,
                Message = "Litige clôturé",
                Data = null,
            }
        );
    }
}
