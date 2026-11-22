using RestaurantHandler.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RestaurantHandler.Persistence.Configurations;

public class HelpEntryConfiguration : IEntityTypeConfiguration<HelpEntry>
{
    public void Configure(EntityTypeBuilder<HelpEntry> builder)
    {
        builder.HasIndex(h => h.Slug).IsUnique();
        builder.HasIndex(h => new { h.Status, h.Language, h.Category, h.SortOrder });

        builder.Property(h => h.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(h => h.Category).HasConversion<string>().HasMaxLength(32);
        builder.Property(h => h.Audience).HasConversion<string>().HasMaxLength(16);

        builder.HasQueryFilter(h => h.ArchivedAt == null);
    }
}
