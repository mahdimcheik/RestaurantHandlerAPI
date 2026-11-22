using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Services.Interfaces;

public interface IReviewService
{
    /// <summary>Crée un avis sur une réservation aboutie de l'élève authentifié.</summary>
    Task<Response<ReviewDetails>> CreateReviewAsync(CreateReview dto, CallerIdentity? principal);

    /// <summary>Synthèse d'un prof : moyenne + total sur tous les avis, liste paginée (First/Rows).</summary>
    Task<Response<TeacherReviewsSummary>> GetTeacherReviewsAsync(Guid teacherId, CustomTableState query);

    /// <summary>Avis de l'élève authentifié pour une réservation, ou null s'il n'en a pas encore laissé.</summary>
    Task<Response<ReviewDetails?>> GetMyReviewForReservationAsync(Guid reservationId, CallerIdentity? principal);

    /// <summary>Modération admin : masque un avis (soft-delete) sans l'effacer.</summary>
    Task<Response<object>> ArchiveReviewAsync(Guid reviewId);

    /// <summary>Modération admin : supprime définitivement un avis.</summary>
    Task<Response<object>> DeleteReviewAsync(Guid reviewId);
}
