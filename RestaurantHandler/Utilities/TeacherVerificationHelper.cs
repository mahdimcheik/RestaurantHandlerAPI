using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Models.Files;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Utilities;

/// <summary>
/// LA règle de vérification documentaire (Feature 10) — source de vérité unique de « ce prof
/// a-t-il fourni tout ce qu'on lui demande ». Recalculée après chaque mutation qui peut la
/// faire varier (upload, revue admin, nouvelle demande ad hoc) plutôt que dérivée
/// paresseusement à la lecture, pour que <see cref="Teacher.IsVerified"/> reste toujours exact.
///
/// Ne sauvegarde volontairement pas : l'appelant garde la main sur <c>SaveChangesAsync</c>.
/// </summary>
public static class TeacherVerificationHelper
{
    /// <summary>
    /// Un type est requis pour CE prof s'il est requis par défaut pour tous les profs
    /// (<see cref="Models.Files.PrivacyDocumentType.IsRequiredByDefault"/>), OU s'il lui a déjà
    /// été demandé un jour via <see cref="Models.Files.TeacherDocumentRequest"/> — même une
    /// demande archivée (comblée) reste une exigence : l'archivage marque juste qu'elle
    /// n'attend plus un nouvel envoi, pas que le document n'est plus requis.
    /// </summary>
    public static async Task RecomputeAsync(MainContext context, Guid teacherId)
    {
        var teacher = await context.Teachers.FirstOrDefaultAsync(t => t.Id == teacherId);
        if (teacher is null)
            return;

        var defaultRequiredTypeIds = await context
            .PrivacyDocumentTypes.Where(t => t.IsRequiredByDefault)
            .Select(t => t.Id)
            .ToListAsync();

        var requestedTypeIds = await context
            .TeacherDocumentRequests.Where(r => r.TeacherId == teacherId)
            .Select(r => r.TypeId)
            .Distinct()
            .ToListAsync();

        var requiredTypeIds = defaultRequiredTypeIds.Union(requestedTypeIds).ToList();

        var validatedTypeIds = await context
            .PrivacyDocuments.Where(d =>
                d.TeacherId == teacherId
                && d.ArchivedAt == null
                && d.Status == DocumentReviewStatusEnum.Validated
                && requiredTypeIds.Contains(d.TypeId)
            )
            .Select(d => d.TypeId)
            .Distinct()
            .ToListAsync();

        var isVerified = requiredTypeIds.Count > 0 && requiredTypeIds.All(validatedTypeIds.Contains);

        if (isVerified == teacher.IsVerified)
            return;

        teacher.IsVerified = isVerified;
        teacher.VerifiedAt = isVerified ? DateTimeOffset.UtcNow : null;
        teacher.UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Crée, à la création du compte, un <see cref="TeacherDocumentRequest"/> (avec
    /// <c>RequestedBy == null</c>) pour chaque <see cref="PrivacyDocumentType.IsRequiredByDefault"/>
    /// — purement pour rendre l'exigence visible au même endroit que les demandes ad hoc
    /// (<c>GET /Teachers/document-requests</c>). <see cref="RecomputeAsync"/> reste la source de
    /// vérité et considère ces types requis même sans cette ligne. Ne sauvegarde volontairement
    /// pas, même convention que <see cref="RecomputeAsync"/>.
    /// </summary>
    public static async Task CreateDefaultDocumentRequestsAsync(MainContext context, Guid teacherId)
    {
        var defaultTypeIds = await context
            .PrivacyDocumentTypes.Where(t => t.IsRequiredByDefault && t.ArchivedAt == null)
            .Select(t => t.Id)
            .ToListAsync();

        foreach (var typeId in defaultTypeIds)
        {
            context.TeacherDocumentRequests.Add(
                new TeacherDocumentRequest
                {
                    TeacherId = teacherId,
                    TypeId = typeId,
                    RequestedById = null,
                }
            );
        }
    }
}
