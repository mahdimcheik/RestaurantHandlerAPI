using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RestaurantHandler.Persistence.Configurations;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        // Au plus une adresse (non archivée) par type et par utilisateur : domicile + travail.
        builder
            .HasIndex(a => new { a.UserId, a.TypeId })
            .IsUnique()
            .HasFilter("\"ArchivedAt\" IS NULL");

        builder.HasQueryFilter(d => d.ArchivedAt == null);

        // Rue et complément chiffrés au repos (ADR-0022) ; ville, code postal, pays et
        // coordonnées (géocodées depuis ville + CP) restent en clair pour la recherche.
        builder.Property(a => a.Street).HasConversion(new EncryptedStringConverter());
        builder.Property(a => a.AdditionalInfo).HasConversion(new EncryptedStringConverter());
    }
}
