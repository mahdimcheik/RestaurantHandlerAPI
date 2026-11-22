using RestaurantHandler.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RestaurantHandler.Persistence.Configurations;

public class ArticleCommentConfiguration : IEntityTypeConfiguration<ArticleComment>
{
    public void Configure(EntityTypeBuilder<ArticleComment> builder)
    {
        builder.HasIndex(c => new { c.ArticleId, c.ParentId, c.CreatedAt });

        builder
            .HasOne(c => c.Article)
            .WithMany(a => a.Comments)
            .HasForeignKey(c => c.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Supprimer un commentaire racine emporte ses réponses.
        builder
            .HasOne(c => c.Parent)
            .WithMany(c => c.Replies)
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Un commentaire masqué (modération admin) sort de toutes les lectures, comme son
        // article archivé.
        builder.HasQueryFilter(c => c.ArchivedAt == null && c.Article!.ArchivedAt == null);
    }
}
