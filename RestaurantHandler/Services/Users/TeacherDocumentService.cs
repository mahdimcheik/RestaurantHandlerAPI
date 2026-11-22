using RestaurantHandler.Contexts;
using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Models;
using RestaurantHandler.Models.Files;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace RestaurantHandler.Services;

public class TeacherDocumentService
{
    private readonly MainContext _context;
    private readonly StorageService _storage;
    private readonly NotificationDispatcher _notifications;
    private readonly HybridCache _cache;

    public TeacherDocumentService(
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

    public async Task<Response<List<PrivacyDocumentDetails>>> GetDocumentsByTeacher(
        CallerIdentity? userPrincipal
    )
    {
        var user = CheckUser.GetUser(userPrincipal, _context);
        if (user == null)
        {
            return new Response<List<PrivacyDocumentDetails>>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
                Data = null,
            };
        }

        var teacher = await _context.Users.FirstOrDefaultAsync(t => t.Id == user.Id);

        if (teacher == null)
        {
            return new Response<List<PrivacyDocumentDetails>>
            {
                Status = 404,
                Message = "Profil enseignant non trouvé",
                Data = null,
            };
        }

        // Seul le document vivant (non supersédé) par type compte pour l'admin/la vérification.
        var documents = await _context
            .PrivacyDocuments.Include(d => d.Type)
            .Where(d => d.TeacherId == user.Id && d.ArchivedAt == null)
            .ToListAsync();

        var documentsDetails = new List<PrivacyDocumentDetails>();
        foreach (var d in documents)
        {
            documentsDetails.Add(
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
                        DisplayName = d.Type.DisplayName,
                        Name = d.Type.Name,
                        Id = d.Type.Id,
                        IsRequiredByDefault = d.Type.IsRequiredByDefault,
                        Color = d.Type.Color,
                        Icon = d.Type.Icon,
                        Translations = d.Type.Translations,
                    },
                }
            );
        }

        return new Response<List<PrivacyDocumentDetails>>
        {
            Status = 200,
            Message = "Documents par utilisateur",
            Data = documentsDetails,
        };
    }

    /// <summary>
    /// Le prof envoie un document (Feature 10). Stocké via <see cref="StorageService"/> :
    /// un envoi supersède le document vivant précédent du même type (archivé, jamais écrasé — trace
    /// conservée pour l'admin) et referme toute demande ouverte pour ce type
    /// (<see cref="TeacherDocumentRequest"/>). Repart toujours en <see cref="DocumentReviewStatusEnum.Pending"/>,
    /// notifie tous les admins, et recalcule <see cref="Teacher.IsVerified"/>.
    /// </summary>
    public async Task<Response<PrivacyDocumentDetails>> UploadPrivacyDocumentAsync(
        UploadPrivacyDocument dto,
        CallerIdentity? userPrincipal
    )
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        var user = CheckUser.GetUser(userPrincipal, _context);
        if (user == null)
        {
            return new Response<PrivacyDocumentDetails>
            {
                Status = 401,
                Message = "Utilisateur non authentifie",
            };
        }

        var documentType = await _context.PrivacyDocumentTypes.FindAsync(dto.TypeId);
        if (documentType == null)
        {
            return new Response<PrivacyDocumentDetails>
            {
                Status = 404,
                Message = "Type de document non trouve",
            };
        }

        var uploadResult = await _storage.Upload(
            dto.File,
            $"teachers/documents/{user.Id}",
            CancellationToken.None
        );
        if (uploadResult.Status != 201 || uploadResult.Data is null)
        {
            return new Response<PrivacyDocumentDetails>
            {
                Status = uploadResult.Status,
                Message = uploadResult.Message,
            };
        }

        // Supersède : un seul document vivant par type. L'ancien reste en base, archivé.
        var previousDocuments = await _context
            .PrivacyDocuments.Where(d =>
                d.TeacherId == user.Id && d.TypeId == dto.TypeId && d.ArchivedAt == null
            )
            .ToListAsync();
        foreach (var previous in previousDocuments)
        {
            previous.ArchivedAt = DateTimeOffset.UtcNow;
        }

        // Referme toute demande admin ouverte pour ce type : comblée par cet envoi.
        var openRequests = await _context
            .TeacherDocumentRequests.Where(r =>
                r.TeacherId == user.Id && r.TypeId == dto.TypeId && r.ArchivedAt == null
            )
            .ToListAsync();
        foreach (var request in openRequests)
        {
            request.ArchivedAt = DateTimeOffset.UtcNow;
        }

        var document = new PrivacyDocument
        {
            FileName = dto.File.FileName,
            FilePath = uploadResult.Data,
            MimeType = StorageService.ContentTypeFor(uploadResult.Data),
            Size = dto.File.Length,
            UploadedAt = DateTimeOffset.UtcNow,
            TeacherId = user.Id,
            Title = dto.Title,
            Description = dto.Description,
            TypeId = dto.TypeId,
            Type = documentType,
            Status = DocumentReviewStatusEnum.Pending,
        };

        _context.PrivacyDocuments.Add(document);
        // Flush avant le recalcul : la requête de vérification lit la base, pas le change
        // tracker — les archivages ci-dessus doivent être visibles avant RecomputeAsync.
        await _context.SaveChangesAsync();

        await TeacherVerificationHelper.RecomputeAsync(_context, user.Id);

        var adminNotifications = (await _notifications.GetAdminRecipientsAsync())
            .Select(admin => new NotificationRequest(
                admin.UserId,
                admin.Email,
                NotificationTypeEnum.Document,
                $"Un enseignant a envoyé un document à vérifier ({documentType.DisplayName})",
                "#0a84ff",
                "pi pi-upload",
                NotificationLinks.AdminTeacherDocuments(user.Id, admin.IsSuperAdmin)
            ))
            .ToArray();
        _notifications.Stage(adminNotifications);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        await _notifications.PublishAsync();

        return new Response<PrivacyDocumentDetails>
        {
            Status = 200,
            Message = "Document telecharge avec succes",
            Data = new PrivacyDocumentDetails
            {
                Id = document.Id,
                FileName = document.FileName,
                FilePath = _storage.Url(document.FilePath) ?? document.FilePath,
                MimeType = document.MimeType,
                Size = document.Size,
                Title = document.Title,
                Description = document.Description,
                Status = document.Status,
                Type = new PrivacyDocumentTypeDetails
                {
                    Id = documentType.Id,
                    Name = documentType.Name,
                    DisplayName = documentType.DisplayName,
                    IsRequiredByDefault = documentType.IsRequiredByDefault,
                    Color = documentType.Color,
                    Icon = documentType.Icon,
                    Translations = documentType.Translations,
                },
            },
        };
    }

    /// <summary>
    /// Le prof retire un envoi encore en attente de revue (Feature 10) : archive le document
    /// (jamais supprimé en base — trace conservée) et rouvre la demande admin qu'il avait comblée,
    /// si elle existe, pour qu'elle réapparaisse dans « Demandes de l'administration ». Un document
    /// déjà <see cref="DocumentReviewStatusEnum.Validated"/> ne peut plus être retiré.
    /// </summary>
    public async Task<Response<object>> DeletePrivacyDocumentAsync(
        Guid documentId,
        CallerIdentity? userPrincipal
    )
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        var user = CheckUser.GetUser(userPrincipal, _context);
        if (user == null)
        {
            return new Response<object>
            {
                Status = 401,
                Message = "Utilisateur non authentifie",
            };
        }

        var document = await _context.PrivacyDocuments.FirstOrDefaultAsync(d =>
            d.Id == documentId && d.TeacherId == user.Id && d.ArchivedAt == null
        );
        if (document is null)
        {
            return new Response<object>
            {
                Status = 404,
                Message = "Document non trouve",
            };
        }

        if (document.Status == DocumentReviewStatusEnum.Validated)
        {
            return new Response<object>
            {
                Status = 400,
                Message = "Ce document a deja ete valide et ne peut plus etre retire",
            };
        }

        document.ArchivedAt = DateTimeOffset.UtcNow;

        // Rouvre la demande la plus récente que cet envoi avait comblée (même type), si elle existe.
        var lastFulfilledRequest = await _context
            .TeacherDocumentRequests.Where(r =>
                r.TeacherId == user.Id && r.TypeId == document.TypeId && r.ArchivedAt != null
            )
            .OrderByDescending(r => r.ArchivedAt)
            .FirstOrDefaultAsync();
        if (lastFulfilledRequest is not null)
        {
            lastFulfilledRequest.ArchivedAt = null;
        }

        await _context.SaveChangesAsync();

        await TeacherVerificationHelper.RecomputeAsync(_context, user.Id);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Response<object> { Status = 200, Message = "Document retire avec succes" };
    }

    /// <summary>Demandes de documents encore ouvertes (non comblées) adressées au prof connecté.</summary>
    public async Task<Response<List<TeacherDocumentRequestDetails>>> GetDocumentRequestsByTeacher(
        CallerIdentity? userPrincipal
    )
    {
        var user = CheckUser.GetUser(userPrincipal, _context);
        if (user == null)
        {
            return new Response<List<TeacherDocumentRequestDetails>>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
            };
        }

        var requests = await _context
            .TeacherDocumentRequests.Include(r => r.Type)
            .Include(r => r.RequestedBy)
            .Where(r => r.TeacherId == user.Id && r.ArchivedAt == null)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return new Response<List<TeacherDocumentRequestDetails>>
        {
            Status = 200,
            Message = "Demandes de documents en attente",
            Data = requests.Select(r => new TeacherDocumentRequestDetails(r)).ToList(),
        };
    }

    public async Task<Response<List<PrivacyDocumentTypeDetails>>> PrivacyDocumentTypesAsync()
    {
        var res = await _cache.GetOrCreateAsync(
            CacheKeys.DOCUMENT_TYPES,
            async ct => await _context
                .PrivacyDocumentTypes.Where(p => p.ArchivedAt == null)
                .Select(p => new PrivacyDocumentTypeDetails
                {
                    Id = p.Id,
                    Name = p.Name,
                    DisplayName = p.DisplayName,
                    IsRequiredByDefault = p.IsRequiredByDefault,
                    Color = p.Color,
                    Icon = p.Icon,
                    Translations = p.Translations,
                })
                .ToListAsync(ct),
            CacheKeys.Reference,
            [CacheKeys.DOCUMENT_TYPES_TAG]
        );

        return new Response<List<PrivacyDocumentTypeDetails>>
        {
            Message = "ok",
            Status = 200,
            Data = res,
        };
    }
}
