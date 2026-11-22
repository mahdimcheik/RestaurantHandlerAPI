using RestaurantHandler.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RestaurantHandler.Persistence.Configurations;

public class RoleAppConfiguration : IEntityTypeConfiguration<RoleApp>
{
    public void Configure(EntityTypeBuilder<RoleApp> builder)
    {
        builder.ToTable("Roles");
    }
}
