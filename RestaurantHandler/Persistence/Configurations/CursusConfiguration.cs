using RestaurantHandler.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RestaurantHandler.Persistence.Configurations;

public class CursusConfiguration : IEntityTypeConfiguration<Cursus>
{
    public void Configure(EntityTypeBuilder<Cursus> builder)
    {
        builder.HasQueryFilter(d => d.ArchivedAt == null);
    }
}
