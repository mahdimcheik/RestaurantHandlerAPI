using RestaurantHandler.Models;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantHandler.Controllers;

/// <summary>
/// Contrôleur pour la gestion des créneaux
/// </summary>
[Produces("application/json")]
[Consumes("application/json")]
[Route("[controller]")]
[ApiController]
[EnableCors]
public class SlotsController(
    SlotsService slotsService,
    ReservationQueryService reservationQueryService,
    BookingService bookingService,
    PresenceService presenceService,
    ReservationCancellationService cancellationService,
    ConversationService conversationService
) : ControllerBase
{
    [Authorize(Roles = "Teacher")]
    [HttpPost("teacher/add")]
    public async Task<ActionResult<Response<SlotDetails>>> AddSlotByTeacher(
        [FromBody] SlotCreate slotDto
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

        var response = await slotsService.AddSlotByTeacherAsync(
            slotDto,
            await this.GetCallerAsync()
        );

        return StatusCode(response.Status, response);
    }

    [Authorize(Roles = "Teacher")]
    [HttpPost("teacher/add-batch")]
    public async Task<ActionResult<Response<SlotBatchResult>>> AddSlotsBatchByTeacher(
        [FromBody] SlotBatchCreate batchDto
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

        var response = await slotsService.AddSlotsBatchByTeacherAsync(
            batchDto,
            await this.GetCallerAsync()
        );

        return StatusCode(response.Status, response);
    }

    [Authorize(Roles = "Teacher")]
    [HttpPut("teacher/update")]
    public async Task<ActionResult<Response<SlotDetails>>> UpdateSlotByTeacher(
        [FromBody] SlotUpdate slotDto
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

        var response = await slotsService.UpdateSlotByTeacherAsync(
            slotDto,
            await this.GetCallerAsync()
        );

        return StatusCode(response.Status, response);
    }

    [Authorize(Roles = "Teacher")]
    [HttpDelete("teacher/remove/{slotId:guid}")]
    public async Task<ActionResult<Response<bool>>> RemoveSlotByTeacher([FromRoute] Guid slotId)
    {
        var response = await slotsService.RemoveSlotByTeacherAsync(
            slotId,
            await this.GetCallerAsync()
        );
        return StatusCode(response.Status, response);
    }

    [Authorize(Roles = "Teacher")]
    [HttpPost("teacher/my-slots")]
    public async Task<ActionResult<Response<List<SlotDetails>>>> GetSlotsByTeacher(
        [FromBody] PeriodTime periodTime
    )
    {
        var response = await slotsService.GetSlotsByTeacherAndDatesAsync(
            await this.GetCallerAsync(),
            periodTime.DateFrom,
            periodTime.DateTo
        );

        return StatusCode(response.Status, response);
    }

    [AllowAnonymous]
    [HttpPost("teacher/{teacherId:guid}/available-slots")]
    public async Task<ActionResult<Response<List<SlotDetails>>>> GetAvailableSlotsByTeacher(
        [FromRoute] Guid teacherId,
        [FromBody] PeriodTime periodTime
    )
    {
        var response = await slotsService.GetSlotsByStudentWithTeacherAsync(
            teacherId,
            periodTime.DateFrom,
            periodTime.DateTo,
            await this.GetCallerAsync()
        );

        return Ok(response);
    }

    [Authorize(Roles = "Student")]
    [HttpPost("student")]
    public async Task<ActionResult<Response<List<SlotDetails>>>> GetReservationsByStudent(
        [FromBody] PeriodTime periodTime
    )
    {
        var response = await reservationQueryService.GetReservationByStudentAsync(
            periodTime.DateFrom,
            periodTime.DateTo,
            await this.GetCallerAsync()
        );

        return StatusCode(response.Status, response);
    }

    [Authorize(Roles = "Student")]
    [HttpPost("student/reservations")]
    public async Task<
        ActionResult<Response<List<ReservationDetails>>>
    > GetReservationsByStudentPaginated([FromBody] CustomTableState query)
    {
        var response = await reservationQueryService.GetReservationByStudentPaginated(
            query,
            await this.GetCallerAsync()
        );

        return StatusCode(response.Status, response);
    }

    [Authorize(Roles = "Teacher")]
    [HttpPost("teacher/reservations")]
    public async Task<
        ActionResult<Response<List<ReservationDetails>>>
    > GetReservationsByTeacherPaginated([FromBody] CustomTableState query)
    {
        var response = await reservationQueryService.GetReservationByTeacherPaginated(
            query,
            await this.GetCallerAsync()
        );

        return StatusCode(response.Status, response);
    }

    [Authorize(Roles = "Admin, SuperAdmin")]
    [HttpPost("admin/reservations")]
    public async Task<
        ActionResult<Response<List<ReservationDetails>>>
    > GetReservationsByAdminPaginated([FromBody] CustomTableState query)
    {
        var response = await reservationQueryService.GetReservationByAdminPaginated(query);
        return StatusCode(response.Status, response);
    }

    [Authorize(Roles = "Student")]
    [HttpGet("student/reservations/pending-count")]
    public async Task<ActionResult<Response<int>>> GetPendingReservationsCountByTeacher()
    {
        var response = await reservationQueryService.GetPendingReservationsCountByStudentAsync(
            await this.GetCallerAsync()
        );
        return StatusCode(response.Status, response);
    }

    [HttpPost("student/book")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<Response<ReservationDetails>>> BookSlotByStudent(
        [FromBody] ReservationCreate reservation
    )
    {
        var response = await bookingService.BookSlotAsync(reservation, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPost("teacher/confirm-reservation")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<Response<object>>> ConfirmBookingByTeacher(
        [FromBody] ReservationUpdateStatus updateStatus
    )
    {
        await bookingService.UpdateReservationStatusAsync(
            updateStatus,
            await this.GetCallerAsync()
        );
        return StatusCode(
            200,
            new Response<object>
            {
                Status = 200,
                Message = "Booking status updated successfully",
                Data = null,
            }
        );
    }

    [HttpPost("validate-reservation")]
    [Authorize(Roles = "Teacher, Admin, SuperAdmin")]
    public async Task<
        ActionResult<Response<(ReservationDetails? Reservation, DateTimeOffset? UnlockTime)>>
    > ValidateReservation([FromBody] ReservationValidate dto)
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

        var response = await presenceService.ValidateReservationAsync(
            dto,
            await this.GetCallerAsync()
        );
        return StatusCode(response.Status, response);
    }

    [HttpGet("reservation/{reservationId:guid}")]
    [Authorize]
    public async Task<ActionResult<Response<ReservationDetails>>> GetReservationById(
        Guid reservationId
    )
    {
        var response = await reservationQueryService.GetReservationById(
            reservationId,
            await this.GetCallerAsync()
        );
        return StatusCode(response.Status, response);
    }

    [HttpGet("conversation/{reservationId:Guid}")]
    [Authorize]
    public async Task<
        ActionResult<Response<List<ConversationDetails>>>
    > GetConversationByReservationId(Guid reservationId)
    {
        var response = await conversationService.GetConversationByReservationIdAsync(
            reservationId,
            await this.GetCallerAsync()
        );
        return StatusCode(response.Status, response);
    }

    [HttpPost("conversation/{reservationId:Guid}/attachments")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(HardCode.ATTACHMENT_MAX_SIZE_BYTES + 1024 * 1024)]
    public async Task<ActionResult<Response<ConversationDetails>>> SendConversationAttachment(
        [FromRoute] Guid reservationId,
        [FromForm] ConversationAttachmentUpload dto,
        CancellationToken ct
    )
    {
        var response = await conversationService.SendAttachmentAsync(
            reservationId,
            dto,
            await this.GetCallerAsync(),
            ct
        );
        return StatusCode(response.Status, response);
    }

    [HttpDelete("teacher/remove-reservation")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<Response<object>>> RemoveReservationByTeacher(Guid reservationId)
    {
        await cancellationService.RemoveReservationByTeacherAsync(
            reservationId,
            await this.GetCallerAsync()
        );
        return StatusCode(
            200,
            new Response<object>
            {
                Status = 200,
                Message = "Reservation removed successfully",
                Data = null,
            }
        );
    }

    /// <summary>
    /// Ce que l'élève récupérerait s'il annulait maintenant (remboursement, part retenue, date
    /// limite avant que l'annulation ne devienne tardive). À appeler avant de confirmer.
    /// </summary>
    [HttpGet("student/reservation/{reservationId:guid}/cancellation-preview")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<Response<CancellationPreview>>> GetCancellationPreview(
        [FromRoute] Guid reservationId
    )
    {
        var preview = await cancellationService.GetCancellationPreviewAsync(
            reservationId,
            await this.GetCallerAsync()
        );
        return StatusCode(
            200,
            new Response<CancellationPreview>
            {
                Status = 200,
                Message = "Aperçu d'annulation calculé",
                Data = preview,
            }
        );
    }

    [HttpDelete("student/remove-reservation")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<Response<object>>> RemoveReservationByStudent(Guid reservationId)
    {
        await cancellationService.RemoveReservationByStudentAsync(
            reservationId,
            await this.GetCallerAsync()
        );
        return StatusCode(
            200,
            new Response<object>
            {
                Status = 200,
                Message = "Reservation removed successfully",
                Data = null,
            }
        );
    }
}
