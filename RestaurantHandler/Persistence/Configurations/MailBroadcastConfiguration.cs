using RestaurantHandler.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RestaurantHandler.Persistence.Configurations;

public class MailBroadcastConfiguration : IEntityTypeConfiguration<MailBroadcast>
{
    public void Configure(EntityTypeBuilder<MailBroadcast> builder)
    {
        builder.HasIndex(b => new { b.SenderId, b.CreatedAt });
        builder.HasIndex(b => b.CreatedAt);

        builder.Property(b => b.SenderKind).HasConversion<string>().HasMaxLength(16);

        // Un compte supprimé garde la trace de ses envois passés côté audit.
        builder.HasOne(b => b.Sender).WithMany().HasForeignKey(b => b.SenderId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MailBroadcastRecipientConfiguration : IEntityTypeConfiguration<MailBroadcastRecipient>
{
    public void Configure(EntityTypeBuilder<MailBroadcastRecipient> builder)
    {
        builder.HasKey(r => new { r.BroadcastId, r.UserId });
        builder.HasIndex(r => r.UserId);

        builder
            .HasOne(r => r.Broadcast)
            .WithMany(b => b.Recipients)
            .HasForeignKey(r => r.BroadcastId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
