using RestaurantHandler.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RestaurantHandler.Persistence.Configurations;

/// <summary>
/// Dispute : dossier de litige ancré sur une réservation. La garde de payout reste
/// portée par Reservation.StatusId == DISPUTED ; cette table porte le contenu du dossier.
/// </summary>
public class DisputeConfiguration : IEntityTypeConfiguration<Dispute>
{
    public void Configure(EntityTypeBuilder<Dispute> builder)
    {
        // Un litige au plus par réservation : une réservation sortie de DISPUTED n'y
        // revient jamais, l'unicité en base couvre la course entre deux ouvertures.
        builder
            .HasIndex(d => d.ReservationId)
            .IsUnique()
            .HasFilter("\"ArchivedAt\" IS NULL");

        // La réservation possède son litige : un hard-delete de la réservation l'emporte.
        builder
            .HasOne(d => d.Reservation)
            .WithMany()
            .HasForeignKey(d => d.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ouvreur (l'élève) : ne pas cascader (on garde la trace même si le compte part).
        builder
            .HasOne(d => d.OpenedBy)
            .WithMany()
            .HasForeignKey(d => d.OpenedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Arbitre (admin/prof) : nullable tant qu'ouvert, ne pas cascader.
        builder
            .HasOne(d => d.ResolvedBy)
            .WithMany()
            .HasForeignKey(d => d.ResolvedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
