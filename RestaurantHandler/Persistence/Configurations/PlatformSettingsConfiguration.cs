using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RestaurantHandler.Persistence.Configurations;

/// <summary>La ligne unique existe dès la migration : le service ne la crée jamais.</summary>
public class PlatformSettingsConfiguration : IEntityTypeConfiguration<PlatformSettings>
{
    public void Configure(EntityTypeBuilder<PlatformSettings> builder)
    {
        builder.HasData(
            new PlatformSettings
            {
                Id = HardCode.PLATFORM_SETTINGS_ID,
                CreatedAt = new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero),
                MaintenanceEnabled = false,
            }
        );
    }
}
