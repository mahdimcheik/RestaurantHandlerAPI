using RestaurantHandler.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RestaurantHandler.Persistence.Configurations;

/// <summary>
/// Review : un avis est ancré sur une réservation (1:1), pas sur le prof
/// directement, pour garantir qu'il porte sur un cours réellement passé.
/// </summary>
public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        // Un seul avis par réservation.
        builder.HasIndex(r => r.ReservationId).IsUnique();

        // La réservation possède son avis : un hard-delete de la réservation
        // emporte l'avis associé.
        builder
            .HasOne(r => r.Reservation)
            .WithOne(res => res.Review)
            .HasForeignKey<Review>(r => r.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Un avis archivé (modération admin) sort des lectures publiques/élève.
        builder.HasQueryFilter(r => r.ArchivedAt == null);
    }
}
