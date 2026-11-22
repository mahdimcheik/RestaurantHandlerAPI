using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models;

/// <summary>
/// DTO de création d'un avis. L'avis est ancré sur une réservation : le prof et
/// l'auteur (élève) en sont dérivés côté serveur, jamais fournis par le client.
/// </summary>
public class CreateReview
{
    [Required(ErrorMessage = "L'identifiant de la réservation est requis")]
    public Guid ReservationId { get; set; }

    [Required(ErrorMessage = "La note est requise")]
    [Range(1, 5, ErrorMessage = "La note doit être comprise entre 1 et 5")]
    public int Rating { get; set; }

    [StringLength(1024, ErrorMessage = "Le commentaire ne peut dépasser 1024 caractères")]
    public string? Comment { get; set; }
}

/// <summary>
/// Détails d'un avis exposés au client. Le prof et l'auteur sont projetés depuis
/// la réservation associée (<c>Slot.TeacherId</c> / <c>StudentId</c>).
/// </summary>
public class ReviewDetails
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public Guid TeacherId { get; set; }
    public Guid StudentId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public string AuthorFirstName { get; set; } = string.Empty;
    public string AuthorLastName { get; set; } = string.Empty;
    public string? AuthorImgUrl { get; set; }

    public ReviewDetails() { }

    public ReviewDetails(Review review)
    {
        Id = review.Id;
        ReservationId = review.ReservationId;
        TeacherId = review.Reservation?.Slot?.TeacherId ?? Guid.Empty;
        StudentId = review.Reservation?.StudentId ?? Guid.Empty;
        Rating = review.Rating;
        Comment = review.Comment;
        CreatedAt = review.CreatedAt;

        var author = review.Reservation?.Student?.User;
        if (author is not null)
        {
            AuthorFirstName = author.FirstName;
            AuthorLastName = author.LastName;
            AuthorImgUrl = author.ImgUrl;
        }
    }
}

/// <summary>
/// Synthèse des avis d'un prof : note moyenne, nombre d'avis et la liste.
/// </summary>
public class TeacherReviewsSummary
{
    public Guid TeacherId { get; set; }
    public double AverageRating { get; set; }
    public int Count { get; set; }
    public List<ReviewDetails> Reviews { get; set; } = [];
}
