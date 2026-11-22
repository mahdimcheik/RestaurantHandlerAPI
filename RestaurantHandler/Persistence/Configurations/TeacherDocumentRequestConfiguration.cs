using RestaurantHandler.Models.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RestaurantHandler.Persistence.Configurations;

/// <summary>
/// TeacherDocumentRequest : document attendu d'UN prof (ad hoc par un admin, ou généré
/// automatiquement à la création du compte pour un type requis par défaut — RequestedBy
/// est alors null). Teacher/Type restent Restrict — dossier de conformité, jamais emporté
/// par un hard-delete accidentel (même esprit que Dispute/Refund ci-dessus). RequestedBy
/// est SetNull (comme PrivacyDocuments.ReviewedById) : la requête reste valide même si
/// l'admin qui l'a faite est supprimé, et elle est déjà nullable pour le cas automatique.
/// </summary>
public class TeacherDocumentRequestConfiguration : IEntityTypeConfiguration<TeacherDocumentRequest>
{
    public void Configure(EntityTypeBuilder<TeacherDocumentRequest> builder)
    {
        builder.HasIndex(r => new { r.TeacherId, r.ArchivedAt });

        builder
            .HasOne(r => r.Teacher)
            .WithMany()
            .HasForeignKey(r => r.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(r => r.Type)
            .WithMany()
            .HasForeignKey(r => r.TypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(r => r.RequestedBy)
            .WithMany()
            .HasForeignKey(r => r.RequestedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
