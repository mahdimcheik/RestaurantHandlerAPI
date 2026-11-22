using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantHandler.Models;

namespace RestaurantHandler.Persistence.Configurations;

public class OpeningHoursConfiguration : IEntityTypeConfiguration<OpeningHours>
{
    public void Configure(EntityTypeBuilder<OpeningHours> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("CK_OpeningHours_Range", "\"End\" > \"Start\""));
        builder.Property(h => h.DayOfWeek).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(h => new { h.DayOfWeek, h.Start });
    }
}

public class ClosureConfiguration : IEntityTypeConfiguration<Closure>
{
    public void Configure(EntityTypeBuilder<Closure> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("CK_Closures_Range", "\"EndLocal\" > \"StartLocal\""));
        // Heure murale locale : pas de conversion UTC.
        builder.Property(c => c.StartLocal).HasColumnType("timestamp without time zone");
        builder.Property(c => c.EndLocal).HasColumnType("timestamp without time zone");
        builder.HasIndex(c => c.EndLocal);
    }
}

public class RestaurantSettingsConfiguration : IEntityTypeConfiguration<RestaurantSettings>
{
    public void Configure(EntityTypeBuilder<RestaurantSettings> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_RestaurantSettings_Singleton", $"\"Id\" = '{RestaurantSettings.SingletonId}'");
            t.HasCheckConstraint(
                "CK_RestaurantSettings_Delays",
                "\"AsapDelayMinutes\" >= 0 AND \"MinLeadMinutes\" >= 0 AND \"ApprovalTimeoutMinutes\" >= 1"
                    + " AND \"PickupGraceMinutes\" >= 0 AND \"OnlinePaymentExpiryMinutes\" >= 1"
            );
            t.HasCheckConstraint(
                "CK_RestaurantSettings_Slots",
                "\"SlotDurationMinutes\" >= 5 AND \"SlotCapacityOrders\" >= 1 AND \"NoShowThreshold\" >= 1"
            );
        });

        builder.HasData(
            new RestaurantSettings { Id = RestaurantSettings.SingletonId, TradeName = "Mon restaurant" }
        );
    }
}

public class RestaurantSettingsHistoryConfiguration : IEntityTypeConfiguration<RestaurantSettingsHistory>
{
    public void Configure(EntityTypeBuilder<RestaurantSettingsHistory> builder)
    {
        builder.Property(h => h.SnapshotJson).HasColumnType("jsonb");
        builder.HasIndex(h => h.ChangedAt);
    }
}
