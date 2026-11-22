using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RestaurantHandler.Persistence.Configurations;

public class CategoryCursusConfiguration : IEntityTypeConfiguration<CategoryCursus>
{
    public void Configure(EntityTypeBuilder<CategoryCursus> builder)
    {
        builder
            .Property(c => c.DescriptionTranslations)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .Metadata.SetValueComparer(MainContext.TranslationsComparer);
    }
}
