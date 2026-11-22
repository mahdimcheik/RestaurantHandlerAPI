using RestaurantHandler.Contexts;
using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Models;
using RestaurantHandler.Models.Files;
using RestaurantHandler.Models.Filters;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace RestaurantHandler.Services;

/// <summary>
/// Service pour la gestion des utilisateurs côté administration.
/// </summary>
public class UserManagementService
{
    private readonly MainContext _context;
    private readonly StorageService _storage;
    private readonly NotificationDispatcher _notifications;
    private readonly HybridCache _cache;

    public UserManagementService(
        MainContext context,
        StorageService storage,
        NotificationDispatcher notifications,
        HybridCache cache
    )
    {
        _context = context;
        _storage = storage;
        _notifications = notifications;
        _cache = cache;
    }

    /// <summary>
    /// Récupère la liste paginée des enseignants avec filtres (rôle, confirmation, nom).
    /// </summary>
    public async Task<Response<List<UserDetails>>> GetTeachersAsync(FilterUserManagement filters)
    {
        var query = _context
            .Users.Include(u => u.Teacher)
            .Where(u => u.Teacher != null && u.ArchivedAt == null);

        if (filters.RoleId is not null)
        {
            // Table UserRoles et non la navigation UserApp.UserRoles, mappée sur une clé fantôme jamais remplie.
            query = query.Where(u => _context.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == filters.RoleId));
        }

        if (filters.IsConfirmed is not null)
        {
            query = query.Where(u => u.Teacher.IsStripeConfirmed == filters.IsConfirmed);
        }

        if (!string.IsNullOrWhiteSpace(filters.Name))
        {
            var name = filters.Name.ToLower();
            query = query.Where(u =>
                (u.FirstName.ToLower() + " " + u.LastName.ToLower()).Contains(name)
                || (u.LastName.ToLower() + " " + u.FirstName.ToLower()).Contains(name)
            );
        }

        var count = await query.CountAsync();

        query = query.OrderBy(u => u.LastName).ThenBy(u => u.Id).Skip(filters.First).Take(filters.Row ?? 10);

        var teachers = await query.ToListAsync();
        foreach (var teacher in teachers)
        {
            if (teacher.ImgUrl is not null)
            {
                teacher.ImgUrl = _storage.Url(teacher.ImgUrl);
            }
        }

        return new Response<List<UserDetails>>
        {
            Status = 200,
            Message = "Enseignants récupérés avec succès",
            Data = teachers.Select(p => new UserDetails(p, null)).ToList(),
            Count = count,
        };
    }

    /// <summary>
    /// Modifie le statut de confirmation d'un enseignant.
    /// </summary>
    public async Task<Response<TeacherDetails>> SetTeacherConfirmationAsync(
        Guid userId,
        bool isConfirmed,
        string? stripeAccount = null
    )
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == userId);
        if (teacher is null)
        {
            return new Response<TeacherDetails>
            {
                Status = 404,
                Message = "Enseignant non trouvé",
            };
        }

        teacher.IsStripeConfirmed = isConfirmed;
        if (!string.IsNullOrEmpty(stripeAccount))
        {
            teacher.StripeAccount = stripeAccount;
        }
        await _context.SaveChangesAsync();

        return new Response<TeacherDetails>
        {
            Status = 200,
            Message = isConfirmed
                ? "Enseignant confirmé avec succès"
                : "Confirmation de l'enseignant retirée avec succès",
            Data = new TeacherDetails(teacher, includeEmail: true),
        };
    }

    /// <summary>
    /// Détail de la vérification SIRET d'un enseignant (dénomination et adresse INSEE),
    /// pour l'examen des comptes signalés <see cref="SiretVerificationStatusEnum.FlaggedForReview"/>.
    /// </summary>
    public async Task<Response<TeacherAdminDetails>> GetTeacherSiretVerificationAsync(Guid userId)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == userId);
        if (teacher is null)
        {
            return new Response<TeacherAdminDetails>
            {
                Status = 404,
                Message = "Enseignant non trouvé",
            };
        }

        return new Response<TeacherAdminDetails>
        {
            Status = 200,
            Message = "Vérification SIRET récupérée",
            Data = new TeacherAdminDetails(teacher),
        };
    }

    /// <summary>
    /// Statuts de compte autorisés pour une modification manuelle par l'administration.
    /// </summary>
    private static readonly HashSet<Guid> AllowedAccountStatuses =
    [
        HardCode.ACCOUNT_ACTIVE,
        HardCode.ACCOUNT_PENDING,
        HardCode.ACCOUNT_BANNED,
    ];

    /// <summary>
    /// Modifie manuellement le statut du compte d'un utilisateur (actif, en attente, banni).
    /// </summary>
    public async Task<Response<UserDetails>> SetUserAccountStatusAsync(Guid userId, Guid statusId)
    {
        if (!AllowedAccountStatuses.Contains(statusId))
        {
            return new Response<UserDetails>
            {
                Status = 400,
                Message = "Statut de compte invalide",
            };
        }

        var user = await _context
            .Users.Include(u => u.Status)
            .FirstOrDefaultAsync(u => u.Id == userId && u.ArchivedAt == null);
        if (user is null)
        {
            return new Response<UserDetails>
            {
                Status = 404,
                Message = "Utilisateur non trouvé",
            };
        }

        user.StatusId = statusId;
        user.UpdatedAt = DateTime.UtcNow;

        // Le jeton d'accès déjà émis reste valable jusqu'à son expiration ; sans jeton de
        // renouvellement, la session s'arrête là.
        if (statusId == HardCode.ACCOUNT_BANNED)
        {
            await _context.RefreshTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync();
        }

        await _context.SaveChangesAsync();

        // Recharge la navigation Status pour refléter le nouveau statut dans la réponse.
        await _context.Entry(user).Reference(u => u.Status).LoadAsync();

        return new Response<UserDetails>
        {
            Status = 200,
            Message = "Statut du compte mis à jour avec succès",
            Data = new UserDetails(user, null),
        };
    }

    /// <summary>
    /// Accorde ou retire le rôle Éditeur à un prof. Renvoie ses rôles après modification. Les
    /// articles déjà rédigés restent en place ; la nouvelle liste de rôles n'entre dans le jeton
    /// qu'au prochain renouvellement, mais <see cref="ArticleService"/> relit le rôle en base.
    /// </summary>
    public async Task<Response<List<RoleDetails>>> SetEditorRoleAsync(Guid userId, bool enabled)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.ArchivedAt == null);
        if (user is null)
            return new Response<List<RoleDetails>> { Status = 404, Message = "Utilisateur non trouvé" };

        var userRoles = _context.UserRoles.Where(ur => ur.UserId == userId);
        if (!await userRoles.AnyAsync(ur => ur.RoleId == HardCode.ROLE_TEACHER))
            return new Response<List<RoleDetails>>
            {
                Status = 400,
                Message = "Seul un professeur peut recevoir le rôle éditeur",
            };

        var current = await userRoles.FirstOrDefaultAsync(ur => ur.RoleId == HardCode.ROLE_EDITOR);
        if (enabled && current is null)
            _context.UserRoles.Add(new IdentityUserRole<Guid> { UserId = userId, RoleId = HardCode.ROLE_EDITOR });
        else if (!enabled && current is not null)
            _context.UserRoles.Remove(current);

        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var roles = await _context
            .Roles.Where(r => _context.UserRoles.Any(ur => ur.UserId == userId && ur.RoleId == r.Id))
            .ToListAsync();

        return new Response<List<RoleDetails>>
        {
            Status = 200,
            Message = enabled ? "Rôle éditeur accordé" : "Rôle éditeur retiré",
            Data = roles.Select(r => new RoleDetails(r)).ToList(),
        };
    }

    // ----------------------------------------------------------------------
    // Vérification documentaire (Feature 10)
    // ----------------------------------------------------------------------

    /// <summary>
    /// Dossier de vérification documentaire d'un prof pour l'admin : documents vivants (avec
    /// revue), demandes ad hoc encore ouvertes, état de vérification.
    /// </summary>
    public async Task<Response<TeacherCertificationOverview>> GetTeacherDocumentsAsync(
        Guid userId,
        Guid adminId
    )
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == userId);
        if (teacher is null)
        {
            return new Response<TeacherCertificationOverview>
            {
                Status = 404,
                Message = "Enseignant non trouvé",
            };
        }

        var documents = await _context
            .PrivacyDocuments.Include(d => d.Type)
            .Where(d => d.TeacherId == teacher.Id && d.ArchivedAt == null)
            .ToListAsync();

        var documentDetails = new List<PrivacyDocumentDetails>();
        foreach (var d in documents)
        {
            documentDetails.Add(
                new PrivacyDocumentDetails
                {
                    Id = d.Id,
                    Title = d.Title,
                    Description = d.Description ?? "",
                    FileName = d.FileName,
                    Size = d.Size,
                    FilePath =
                        d.FilePurgedAt is null ? _storage.Url(d.FilePath) ?? d.FilePath : "",
                    Purged = d.FilePurgedAt is not null,
                    MimeType = d.MimeType,
                    Status = d.Status,
                    ReviewedAt = d.ReviewedAt,
                    RefusalReason = d.RefusalReason,
                    Type = new PrivacyDocumentTypeDetails
                    {
                        Id = d.Type.Id,
                        Name = d.Type.Name,
                        DisplayName = d.Type.DisplayName,
                        IsRequiredByDefault = d.Type.IsRequiredByDefault,
                        Color = d.Type.Color,
                        Icon = d.Type.Icon,
                        Translations = d.Type.Translations,
                    },
                }
            );
        }

        // Chaque URL signée remise à l'admin est tracée (RGPD : qui a vu quel justificatif).
        foreach (var d in documents.Where(d => d.FilePurgedAt is null))
        {
            _context.PrivacyDocumentAccessLogs.Add(
                new PrivacyDocumentAccessLog { DocumentId = d.Id, AdminId = adminId }
            );
        }
        await _context.SaveChangesAsync();

        var openRequests = await _context
            .TeacherDocumentRequests.Include(r => r.Type)
            .Include(r => r.RequestedBy)
            .Where(r => r.TeacherId == teacher.Id && r.ArchivedAt == null)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return new Response<TeacherCertificationOverview>
        {
            Status = 200,
            Message = "Dossier de vérification récupéré",
            Data = new TeacherCertificationOverview
            {
                IsVerified = teacher.IsVerified,
                VerifiedAt = teacher.VerifiedAt,
                Documents = documentDetails,
                OpenRequests = openRequests
                    .Select(r => new TeacherDocumentRequestDetails(r))
                    .ToList(),
            },
        };
    }

    /// <summary>
    /// L'admin valide ou refuse un document envoyé. Notifie le prof et recalcule
    /// <see cref="Teacher.IsVerified"/> (une revue peut le faire basculer dans les deux sens).
    /// </summary>
    public async Task<Response<PrivacyDocumentDetails>> ReviewDocumentAsync(
        Guid documentId,
        DocumentReviewDecision dto,
        CallerIdentity? adminPrincipal
    )
    {
        if (dto.Status == DocumentReviewStatusEnum.Pending)
        {
            return new Response<PrivacyDocumentDetails>
            {
                Status = 400,
                Message = "Le statut doit être Validated ou Refused",
            };
        }
        if (
            dto.Status == DocumentReviewStatusEnum.Refused
            && string.IsNullOrWhiteSpace(dto.RefusalReason)
        )
        {
            return new Response<PrivacyDocumentDetails>
            {
                Status = 400,
                Message = "Un motif est requis pour refuser un document",
            };
        }

        var admin = CheckUser.GetUser(adminPrincipal, _context);
        if (admin is null)
        {
            return new Response<PrivacyDocumentDetails>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
            };
        }

        var document = await _context
            .PrivacyDocuments.Include(d => d.Type)
            .Include(d => d.Teacher)
            .ThenInclude(t => t!.User)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.ArchivedAt == null);
        if (document is null)
        {
            return new Response<PrivacyDocumentDetails>
            {
                Status = 404,
                Message = "Document non trouvé",
            };
        }

        document.Status = dto.Status;
        document.ReviewedById = admin.Id;
        document.ReviewedAt = DateTimeOffset.UtcNow;
        document.RefusalReason =
            dto.Status == DocumentReviewStatusEnum.Refused ? dto.RefusalReason : null;
        await _context.SaveChangesAsync();

        // Flush avant recalcul : RecomputeAsync relit la base, pas le change tracker.
        await TeacherVerificationHelper.RecomputeAsync(_context, document.TeacherId!.Value);

        var message =
            dto.Status == DocumentReviewStatusEnum.Validated
                ? $"Votre document {document.Type.DisplayName} a été validé"
                : $"Votre document {document.Type.DisplayName} a été refusé : {dto.RefusalReason}";

        var validated = dto.Status == DocumentReviewStatusEnum.Validated;
        await _notifications.NotifyAsync(
            new NotificationRequest(
                document.TeacherId!.Value,
                document.Teacher?.User?.Email,
                NotificationTypeEnum.Document,
                message,
                validated ? "#34c759" : "#ff3b30",
                validated ? "pi pi-check-circle" : "pi pi-times-circle",
                NotificationLinks.TeacherDocuments
            )
        );

        return new Response<PrivacyDocumentDetails>
        {
            Status = 200,
            Message = "Document revu avec succès",
            Data = new PrivacyDocumentDetails
            {
                Id = document.Id,
                Title = document.Title,
                Description = document.Description,
                FileName = document.FileName,
                FilePath = document.FilePath,
                MimeType = document.MimeType,
                Size = document.Size,
                Status = document.Status,
                ReviewedAt = document.ReviewedAt,
                RefusalReason = document.RefusalReason,
                Type = new PrivacyDocumentTypeDetails
                {
                    Id = document.Type.Id,
                    Name = document.Type.Name,
                    DisplayName = document.Type.DisplayName,
                    IsRequiredByDefault = document.Type.IsRequiredByDefault,
                    Color = document.Type.Color,
                    Icon = document.Type.Icon,
                    Translations = document.Type.Translations,
                },
            },
        };
    }

    /// <summary>
    /// L'admin demande un document supplémentaire à UN prof spécifique — n'affecte que ce
    /// prof (cf. <see cref="TeacherDocumentRequest"/>). Notifie le prof et recalcule
    /// <see cref="Teacher.IsVerified"/> (la nouvelle exigence peut le faire repasser à false).
    /// </summary>
    public async Task<Response<TeacherDocumentRequestDetails>> RequestDocumentAsync(
        Guid userId,
        DocumentRequestCreate dto,
        CallerIdentity? adminPrincipal
    )
    {
        var admin = CheckUser.GetUser(adminPrincipal, _context);
        if (admin is null)
        {
            return new Response<TeacherDocumentRequestDetails>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
            };
        }

        var teacher = await _context
            .Teachers.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.UserId == userId);
        if (teacher is null)
        {
            return new Response<TeacherDocumentRequestDetails>
            {
                Status = 404,
                Message = "Enseignant non trouvé",
            };
        }

        var documentType = await _context.PrivacyDocumentTypes.FirstOrDefaultAsync(t =>
            t.Id == dto.TypeId && t.ArchivedAt == null
        );
        if (documentType is null)
        {
            return new Response<TeacherDocumentRequestDetails>
            {
                Status = 404,
                Message = "Type de document non trouvé",
            };
        }

        var request = new TeacherDocumentRequest
        {
            TeacherId = teacher.Id,
            TypeId = dto.TypeId,
            RequestedById = admin.Id,
            Note = dto.Note,
        };
        _context.TeacherDocumentRequests.Add(request);
        await _context.SaveChangesAsync();

        await TeacherVerificationHelper.RecomputeAsync(_context, teacher.Id);
        await _context.SaveChangesAsync();

        var message = string.IsNullOrWhiteSpace(dto.Note)
            ? $"L'administration vous demande un document supplémentaire : {documentType.DisplayName}"
            : $"L'administration vous demande un document supplémentaire : {documentType.DisplayName} — {dto.Note}";

        await _notifications.NotifyAsync(
            new NotificationRequest(
                teacher.Id,
                teacher.User?.Email,
                NotificationTypeEnum.Document,
                message,
                "#ff9500",
                "pi pi-file-plus",
                NotificationLinks.TeacherDocuments
            )
        );

        request.Type = documentType;
        request.RequestedBy = admin;

        return new Response<TeacherDocumentRequestDetails>
        {
            Status = 201,
            Message = "Document demandé avec succès",
            Data = new TeacherDocumentRequestDetails(request),
        };
    }

    /// <summary>
    /// Annule une demande jamais comblée : contrairement à un envoi (qui archive la demande,
    /// « comblée »), une annulation la retire — elle ne doit plus compter dans les exigences
    /// du prof (cf. <see cref="TeacherVerificationHelper"/>).
    /// </summary>
    public async Task<Response<object>> CancelDocumentRequestAsync(Guid requestId)
    {
        var request = await _context.TeacherDocumentRequests.FirstOrDefaultAsync(r =>
            r.Id == requestId && r.ArchivedAt == null
        );
        if (request is null)
        {
            return new Response<object> { Status = 404, Message = "Demande non trouvée" };
        }

        var teacherId = request.TeacherId;
        _context.TeacherDocumentRequests.Remove(request);
        await _context.SaveChangesAsync();

        await TeacherVerificationHelper.RecomputeAsync(_context, teacherId);
        await _context.SaveChangesAsync();

        return new Response<object> { Status = 200, Message = "Demande annulée" };
    }

    // ----------------------------------------------------------------------
    // Catalogue des types de document (admin)
    // ----------------------------------------------------------------------

    public async Task<Response<PrivacyDocumentTypeDetails>> CreateDocumentTypeAsync(
        PrivacyDocumentTypeCreate dto
    )
    {
        var type = new PrivacyDocumentType
        {
            Name = dto.Name,
            DisplayName = TranslationsHelper.DefaultLabel(dto.Translations),
            Color = dto.Color,
            Icon = dto.Icon,
            IsRequiredByDefault = dto.IsRequiredByDefault,
            Translations = TranslationsHelper.Normalize(dto.Translations),
        };
        _context.PrivacyDocumentTypes.Add(type);
        await _context.SaveChangesAsync();
        await _cache.RemoveByTagAsync(CacheKeys.DOCUMENT_TYPES_TAG);

        return new Response<PrivacyDocumentTypeDetails>
        {
            Status = 201,
            Message = "Type de document créé",
            Data = ToDetails(type),
        };
    }

    public async Task<Response<PrivacyDocumentTypeDetails>> UpdateDocumentTypeAsync(
        Guid id,
        PrivacyDocumentTypeUpdate dto
    )
    {
        var type = await _context.PrivacyDocumentTypes.FirstOrDefaultAsync(t =>
            t.Id == id && t.ArchivedAt == null
        );
        if (type is null)
        {
            return new Response<PrivacyDocumentTypeDetails>
            {
                Status = 404,
                Message = "Type de document non trouvé",
            };
        }

        type.Name = dto.Name;
        type.DisplayName = TranslationsHelper.DefaultLabel(dto.Translations);
        type.Color = dto.Color;
        type.Icon = dto.Icon;
        type.IsRequiredByDefault = dto.IsRequiredByDefault;
        type.Translations = TranslationsHelper.Normalize(dto.Translations);
        type.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();
        await _cache.RemoveByTagAsync(CacheKeys.DOCUMENT_TYPES_TAG);

        return new Response<PrivacyDocumentTypeDetails>
        {
            Status = 200,
            Message = "Type de document mis à jour",
            Data = ToDetails(type),
        };
    }

    public async Task<Response<object>> DeleteDocumentTypeAsync(Guid id)
    {
        var type = await _context.PrivacyDocumentTypes.FirstOrDefaultAsync(t =>
            t.Id == id && t.ArchivedAt == null
        );
        if (type is null)
        {
            return new Response<object> { Status = 404, Message = "Type de document non trouvé" };
        }

        type.ArchivedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();
        await _cache.RemoveByTagAsync(CacheKeys.DOCUMENT_TYPES_TAG);

        return new Response<object> { Status = 200, Message = "Type de document supprimé" };
    }

    private static PrivacyDocumentTypeDetails ToDetails(PrivacyDocumentType type) =>
        new()
        {
            Id = type.Id,
            Name = type.Name,
            DisplayName = type.DisplayName,
            IsRequiredByDefault = type.IsRequiredByDefault,
            Color = type.Color,
            Icon = type.Icon,
            Translations = type.Translations,
        };
}
