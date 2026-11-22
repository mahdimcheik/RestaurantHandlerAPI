using RestaurantHandler.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RestaurantHandler.Persistence.Configurations;

public class FormationConfiguration : IEntityTypeConfiguration<Formation>
{
    public void Configure(EntityTypeBuilder<Formation> builder)
    {
        builder.HasQueryFilter(d => d.ArchivedAt == null);
    }
}
