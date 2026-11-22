using RestaurantHandler.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RestaurantHandler.Persistence.Configurations;

public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        // Sert le NOT EXISTS « ce créneau a-t-il une réservation bloquante ? », évalué pour
        // chaque créneau candidat du filtre ci-dessus.
        builder.HasIndex(r => new { r.SlotId, r.StatusId });
    }
}
