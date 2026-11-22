using RestaurantHandler.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RestaurantHandler.Persistence.Configurations;

/// <summary>
/// ReservationStatusHistory : journal append-only des transitions de statut.
/// </summary>
public class ReservationStatusHistoryConfiguration : IEntityTypeConfiguration<ReservationStatusHistory>
{
    public void Configure(EntityTypeBuilder<ReservationStatusHistory> builder)
    {
        // Récupérer la timeline d'une réservation, triée chronologiquement.
        builder.HasIndex(h => new { h.ReservationId, h.CreatedAt });

        // La réservation possède son historique : un hard-delete nettoie le log.
        builder
            .HasOne(h => h.Reservation)
            .WithMany(r => r.StatusHistory)
            .HasForeignKey(h => h.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK vers les statuts de référence : ne jamais cascader (table de lookup).
        builder
            .HasOne(h => h.FromStatus)
            .WithMany()
            .HasForeignKey(h => h.FromStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(h => h.ToStatus)
            .WithMany()
            .HasForeignKey(h => h.ToStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        // Acteur de la transition : nullable (système/cron), ne pas cascader.
        builder
            .HasOne(h => h.ChangedBy)
            .WithMany()
            .HasForeignKey(h => h.ChangedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
