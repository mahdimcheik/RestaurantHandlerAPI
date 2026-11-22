using RestaurantHandler.Models.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RestaurantHandler.Persistence.Configurations;

/// <summary>
/// PrivacyDocument : ajoute la revue admin par-dessus le fichier envoyé (Feature 10).
/// Pas d'index composite (TeacherId, TypeId) : en TPT, TeacherId vit sur "Documents" et
/// TypeId sur "PrivacyDocuments" — deux tables différentes, EF refuse l'index. Les index
/// FK par convention sur chaque colonne (déjà posés) suffisent aux requêtes du service.
/// </summary>
public class PrivacyDocumentConfiguration : IEntityTypeConfiguration<PrivacyDocument>
{
    public void Configure(EntityTypeBuilder<PrivacyDocument> builder)
    {
        builder.ToTable("PrivacyDocuments");

        // Admin ayant revu : nullable tant qu'en attente, ne pas cascader.
        builder.HasOne(d => d.ReviewedBy)
            .WithMany()
            .HasForeignKey(d => d.ReviewedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
