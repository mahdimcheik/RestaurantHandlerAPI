using RestaurantHandler.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RestaurantHandler.Persistence.Configurations;

public class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> builder)
    {
        builder.HasIndex(a => a.Slug).IsUnique();
        builder.HasIndex(a => new { a.Status, a.Language, a.PublishedAt });
        builder.HasIndex(a => a.Tags).HasMethod("gin");

        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(16);

        builder.OwnsOne(
            a => a.Content,
            content =>
            {
                content.ToJson();
                content.OwnsMany(c => c.Sections);
                content.OwnsMany(c => c.References);
            }
        );

        // Le compte de l'auteur peut disparaître, l'article reste signé par AuthorDisplayName.
        builder
            .HasOne(a => a.Author)
            .WithMany()
            .HasForeignKey(a => a.AuthorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasQueryFilter(a => a.ArchivedAt == null);
    }
}
