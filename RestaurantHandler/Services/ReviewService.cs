using RestaurantHandler.Contexts;
using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Models;
using RestaurantHandler.Services.Interfaces;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

/// <summary>
/// Service de gestion des avis. Un avis est ancré sur une réservation aboutie
/// (<c>RESERVATION_DONE</c>) appartenant à l'élève : c'est ce qui garantit qu'il
/// porte sur un cours réellement passé et n'est pas falsifiable.
/// </summary>
public class ReviewService(MainContext context, StorageService storage) : IReviewService
{
    /// <summary>
    /// L'avatar de l'auteur est stocké en chemin relatif : le résoudre en URL
    /// publique avant de renvoyer le DTO au client.
    /// </summary>
    private void ResolveAuthorAvatar(ReviewDetails details)
    {
        if (!string.IsNullOrEmpty(details.AuthorImgUrl))
        {
            details.AuthorImgUrl = storage.Url(details.AuthorImgUrl);
        }
    }

    /// <summary>
    /// Recalcule la note dénormalisée du prof (<see cref="Teacher.AverageRating"/> et
    /// <see cref="Teacher.ReviewsCount"/>) depuis ses avis, après chaque mutation
    /// (création / archivage / suppression). On recalcule depuis la source plutôt
    /// qu'en incrémental : c'est le seul moyen correct quand un avis disparaît
    /// (modération, suppression). Le filtre global exclut déjà les avis archivés,
    /// donc la note stockée reflète exactement ce que voit le public. Vaut 0 sans avis.
    /// </summary>
    private async Task RecomputeTeacherRatingAsync(Guid teacherId)
    {
        var stats = await context
            .Reviews.Where(r => r.Reservation!.Slot!.TeacherId == teacherId)
            .GroupBy(r => 1)
            .Select(g => new { Avg = g.Average(r => (double)r.Rating), Count = g.Count() })
            .FirstOrDefaultAsync();

        var teacher = await context.Teachers.FirstOrDefaultAsync(t => t.Id == teacherId);
        if (teacher is null)
        {
            return;
        }

        teacher.AverageRating = stats is null ? 0 : Math.Round(stats.Avg, 2);
        teacher.ReviewsCount = stats?.Count ?? 0;
        await context.SaveChangesAsync();
    }

    public async Task<Response<ReviewDetails>> CreateReviewAsync(
        CreateReview dto,
        CallerIdentity? principal
    )
    {
        var user = CheckUser.GetUser(principal, context);
        if (user is null)
        {
            return new Response<ReviewDetails>
            {
                Status = 404,
                Message = "L'utilisateur n'existe pas",
                Data = null,
            };
        }

        // La réservation porte l'élève (StudentId) et le prof (via Slot.TeacherId).
        var reservation = await context
            .Reservations.Include(r => r.Slot)
            .Include(r => r.Student)
            .ThenInclude(s => s!.User)
            .FirstOrDefaultAsync(r => r.Id == dto.ReservationId);

        if (reservation is null)
        {
            return new Response<ReviewDetails>
            {
                Status = 404,
                Message = "La réservation n'existe pas",
                Data = null,
            };
        }

        // Seul l'élève propriétaire de la réservation peut la noter.
        if (reservation.StudentId != user.Id)
        {
            return new Response<ReviewDetails>
            {
                Status = 404,
                Message = "La réservation n'existe pas",
                Data = null,
            };
        }

        // On ne note qu'un cours réellement passé.
        // DONE est le seul état qui l'atteste (I12) : professeur versé, ou créneau terminé
        // en flux sans paiement.
        if (reservation.StatusId != HardCode.RESERVATION_DONE)
        {
            return new Response<ReviewDetails>
            {
                Status = 400,
                Message = "Seul un cours abouti peut être noté",
                Data = null,
            };
        }

        // Un seul avis par réservation (l'index unique couvre la course en BD).
        var alreadyReviewed = await context.Reviews.AnyAsync(r =>
            r.ReservationId == dto.ReservationId
        );
        if (alreadyReviewed)
        {
            return new Response<ReviewDetails>
            {
                Status = 409,
                Message = "Cette réservation a déjà été notée",
                Data = null,
            };
        }

        var review = new Review
        {
            Id = Guid.NewGuid(),
            ReservationId = dto.ReservationId,
            Rating = dto.Rating,
            Comment = dto.Comment,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        context.Reviews.Add(review);
        await context.SaveChangesAsync();

        // Met à jour la note dénormalisée du prof (créneau → TeacherId).
        await RecomputeTeacherRatingAsync(reservation.Slot!.TeacherId);

        // Rattache la réservation chargée pour la projection (prof + auteur).
        review.Reservation = reservation;

        var details = new ReviewDetails(review);
        ResolveAuthorAvatar(details);

        return new Response<ReviewDetails>
        {
            Status = 201,
            Message = "Avis enregistré avec succès",
            Data = details,
        };
    }

    public async Task<Response<TeacherReviewsSummary>> GetTeacherReviewsAsync(
        Guid teacherId,
        CustomTableState query
    )
    {
        var teacherExists = await context.Teachers.AnyAsync(t => t.Id == teacherId);
        if (!teacherExists)
        {
            return new Response<TeacherReviewsSummary>
            {
                Status = 404,
                Message = "L'enseignant n'existe pas",
                Data = null,
            };
        }

        // Le prof se dérive via Reservation.Slot.TeacherId.
        var baseQuery = context.Reviews.Where(r => r.Reservation!.Slot!.TeacherId == teacherId);

        // Moyenne et total portent sur l'ensemble des avis, pas sur la page.
        var count = await baseQuery.CountAsync();
        var average = count > 0 ? await baseQuery.AverageAsync(r => r.Rating) : 0;

        // Pagination simple : First (offset) + Rows (taille de page).
        if (query.Rows <= 0)
        {
            query.Rows = 10;
        }

        var page = await baseQuery
            .Include(r => r.Reservation)
            .ThenInclude(res => res!.Slot)
            .Include(r => r.Reservation)
            .ThenInclude(res => res!.Student)
            .ThenInclude(s => s!.User)
            .OrderByDescending(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .Skip(query.First)
            .Take(query.Rows)
            .ToListAsync();

        var summary = new TeacherReviewsSummary
        {
            TeacherId = teacherId,
            Count = count,
            AverageRating = Math.Round(average, 2),
            Reviews = page.Select(r => new ReviewDetails(r)).ToList(),
        };

        // Résout les avatars relatifs des auteurs en URL publiques.
        foreach (var details in summary.Reviews)
        {
            ResolveAuthorAvatar(details);
        }

        return new Response<TeacherReviewsSummary>
        {
            Status = 200,
            Message = "Avis récupérés avec succès",
            Data = summary,
            Count = count,
        };
    }

    public async Task<Response<ReviewDetails?>> GetMyReviewForReservationAsync(
        Guid reservationId,
        CallerIdentity? principal
    )
    {
        var user = CheckUser.GetUser(principal, context);
        if (user is null)
        {
            return new Response<ReviewDetails?>
            {
                Status = 404,
                Message = "L'utilisateur n'existe pas",
                Data = null,
            };
        }

        var review = await context
            .Reviews.Include(r => r.Reservation)
            .ThenInclude(res => res!.Slot)
            .Include(r => r.Reservation)
            .ThenInclude(res => res!.Student)
            .ThenInclude(s => s!.User)
            .FirstOrDefaultAsync(r =>
                r.ReservationId == reservationId && r.Reservation!.StudentId == user.Id
            );

        ReviewDetails? details = null;
        if (review is not null)
        {
            details = new ReviewDetails(review);
            ResolveAuthorAvatar(details);
        }

        return new Response<ReviewDetails?>
        {
            Status = 200,
            Message = review is null ? "Aucun avis pour cette réservation" : "Avis récupéré",
            Data = details,
        };
    }

    public async Task<Response<object>> ArchiveReviewAsync(Guid reviewId)
    {
        // Le filtre global exclut déjà les avis archivés : on ne ré-archive pas.
        // On charge le créneau pour récupérer le prof à recalculer ensuite.
        var review = await context
            .Reviews.Include(r => r.Reservation)
            .ThenInclude(res => res!.Slot)
            .FirstOrDefaultAsync(r => r.Id == reviewId);
        if (review is null)
        {
            return new Response<object>
            {
                Status = 404,
                Message = "L'avis n'existe pas ou est déjà archivé",
                Data = null,
            };
        }

        review.ArchivedAt = DateTimeOffset.UtcNow;
        review.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();

        // L'avis archivé sort des lectures : la note du prof doit le refléter.
        await RecomputeTeacherRatingAsync(review.Reservation!.Slot!.TeacherId);

        return new Response<object>
        {
            Status = 200,
            Message = "Avis archivé avec succès",
            Data = null,
        };
    }

    public async Task<Response<object>> DeleteReviewAsync(Guid reviewId)
    {
        // IgnoreQueryFilters : l'admin peut aussi purger un avis déjà archivé.
        // On charge le créneau pour récupérer le prof à recalculer ensuite.
        var review = await context
            .Reviews.IgnoreQueryFilters()
            .Include(r => r.Reservation)
            .ThenInclude(res => res!.Slot)
            .FirstOrDefaultAsync(r => r.Id == reviewId);
        if (review is null)
        {
            return new Response<object>
            {
                Status = 404,
                Message = "L'avis n'existe pas",
                Data = null,
            };
        }

        var teacherId = review.Reservation!.Slot!.TeacherId;
        context.Reviews.Remove(review);
        await context.SaveChangesAsync();

        // L'avis supprimé sort du calcul : on rafraîchit la note du prof.
        await RecomputeTeacherRatingAsync(teacherId);

        return new Response<object>
        {
            Status = 200,
            Message = "Avis supprimé définitivement",
            Data = null,
        };
    }
}
