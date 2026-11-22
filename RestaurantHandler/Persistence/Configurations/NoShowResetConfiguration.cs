using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantHandler.Models;

namespace RestaurantHandler.Persistence.Configurations;

public class NoShowResetConfiguration : IEntityTypeConfiguration<NoShowReset>
{
    public void Configure(EntityTypeBuilder<NoShowReset> builder)
    {
        builder.HasIndex(r => new { r.UserId, r.ResetAt });
        builder.HasIndex(r => r.ResetByUserId);

        // Trace d'audit : elle ne doit pas disparaître avec un compte.
        builder.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.ResetByUser).WithMany().HasForeignKey(r => r.ResetByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
