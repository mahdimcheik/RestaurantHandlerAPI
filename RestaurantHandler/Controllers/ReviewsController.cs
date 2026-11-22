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
public class ReviewsController(IReviewService reviewService) : ControllerBase
{
    /// <summary>Un élève note une de ses réservations abouties.</summary>
    [HttpPost("add")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<Response<ReviewDetails>>> AddReview([FromBody] CreateReview dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new Response<object>
            {
                Status = 400,
                Message = "Données de validation invalides",
                Data = ModelState,
            });
        }

        var response = await reviewService.CreateReviewAsync(dto, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    /// <summary>Liste publique paginée des avis d'un prof + note moyenne (First/Rows).</summary>
    [HttpPost("teacher/{teacherId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<Response<TeacherReviewsSummary>>> GetTeacherReviews(
        [FromRoute] Guid teacherId,
        [FromBody] CustomTableState? query = null
    )
    {
        // Sans corps : première page par défaut (First = 0, Rows par défaut côté service).
        var response = await reviewService.GetTeacherReviewsAsync(teacherId, query ?? new CustomTableState());
        return StatusCode(response.Status, response);
    }

    /// <summary>Avis de l'élève authentifié pour une réservation (pour piloter l'UI).</summary>
    [HttpGet("reservation/{reservationId:guid}/mine")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<Response<ReviewDetails?>>> GetMyReview(
        [FromRoute] Guid reservationId
    )
    {
        var response = await reviewService.GetMyReviewForReservationAsync(reservationId, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    /// <summary>Modération admin : masque un avis sans l'effacer (soft-delete).</summary>
    [HttpPatch("{reviewId:guid}/archive")]
    [Authorize(Roles = "Admin, SuperAdmin")]
    public async Task<ActionResult<Response<object>>> ArchiveReview([FromRoute] Guid reviewId)
    {
        var response = await reviewService.ArchiveReviewAsync(reviewId);
        return StatusCode(response.Status, response);
    }

    /// <summary>Modération admin : supprime définitivement un avis.</summary>
    [HttpDelete("{reviewId:guid}")]
    [Authorize(Roles = "Admin, SuperAdmin")]
    public async Task<ActionResult<Response<object>>> DeleteReview([FromRoute] Guid reviewId)
    {
        var response = await reviewService.DeleteReviewAsync(reviewId);
        return StatusCode(response.Status, response);
    }
}
