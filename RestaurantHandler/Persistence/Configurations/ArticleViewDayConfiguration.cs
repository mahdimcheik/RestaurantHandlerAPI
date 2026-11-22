using RestaurantHandler.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RestaurantHandler.Persistence.Configurations;

public class ArticleViewDayConfiguration : IEntityTypeConfiguration<ArticleViewDay>
{
    public void Configure(EntityTypeBuilder<ArticleViewDay> builder)
    {
        builder.HasKey(v => new { v.ArticleId, v.Day });
        builder.HasIndex(v => v.Day);

        builder
            .HasOne(v => v.Article)
            .WithMany()
            .HasForeignKey(v => v.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(v => v.Article!.ArchivedAt == null);
    }
}
