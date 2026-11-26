using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantHandler.Models;

namespace RestaurantHandler.Persistence.Configurations;

public class BusinessProfileConfiguration : IEntityTypeConfiguration<BusinessProfile>
{
    public void Configure(EntityTypeBuilder<BusinessProfile> builder)
    {
        builder
            .HasOne(p => p.User)
            .WithOne()
            .HasForeignKey<BusinessProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(p => p.Siren);
    }
}

public class InvoiceRequestConfiguration : IEntityTypeConfiguration<InvoiceRequest>
{
    public void Configure(EntityTypeBuilder<InvoiceRequest> builder)
    {
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);

        // Une seule demande par commande : une confirmation de paiement rejouée n'en crée pas d'autre.
        builder.HasIndex(r => r.OrderId).IsUnique();
        builder.HasIndex(r => new { r.Status, r.CreatedAt });

        builder
            .HasOne(r => r.Order)
            .WithMany()
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
