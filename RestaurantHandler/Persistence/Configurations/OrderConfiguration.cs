using RestaurantHandler.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RestaurantHandler.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        // Human-readable order number (BP_yyyyMMdd_NNN) must be globally unique. This
        // index is also the concurrency guard: GetActiveOrder derives the daily sequence
        // from a count then retries on this constraint's violation, so two orders created
        // in the same instant can never end up with the same number.
        builder
            .HasIndex(o => o.OrderNumber)
            .IsUnique();

        // Order <-> Payment is 1:1 with the FK on the dependent (Payment.OrderId).
        // Explicit config so EF treats the two navigations as a single relationship.
        builder
            .HasOne(o => o.Payment)
            .WithOne(p => p.Order)
            .HasForeignKey<Payment>(p => p.OrderId);

        // Order is teacher-scoped (one order = one teacher). Restrict so a teacher
        // carrying orders is never cascade-deleted, matching the other teacher FKs.
        builder
            .HasOne(o => o.Teacher)
            .WithMany()
            .HasForeignKey(o => o.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
