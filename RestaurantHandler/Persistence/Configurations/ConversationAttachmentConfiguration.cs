using RestaurantHandler.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RestaurantHandler.Persistence.Configurations;

public class ConversationAttachmentConfiguration : IEntityTypeConfiguration<ConversationAttachment>
{
    public void Configure(EntityTypeBuilder<ConversationAttachment> builder)
    {
        builder.ToTable("ConversationAttachments");

        builder.HasIndex(a => a.ConversationId).IsUnique();

        builder
            .HasOne(a => a.Conversation)
            .WithOne(c => c.Attachment)
            .HasForeignKey<ConversationAttachment>(a => a.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
