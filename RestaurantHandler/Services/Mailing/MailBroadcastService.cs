using RestaurantHandler.Contexts;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

/// <summary>
/// Envois groupés par e-mail (ADR-0023). Un admin écrit à un ou plusieurs publics. L'envoi est
/// historisé puis éclaté en un mail par destinataire dans Hangfire : la requête ne paie pas le
/// rendu ni le SMTP.
/// </summary>
public class MailBroadcastService(
    MainContext context,
    MailService mailService,
    RichTextSanitizer sanitizer,
    ILogger<MailBroadcastService> logger
)
{
    private const int DefaultRows = 20;
    private const int MaxRows = 100;

    // ---- Administration ---------------------------------------------------------------------

    public async Task<Response<int>> CountAdminAudienceAsync(AdminMailAudienceCount dto, CallerIdentity? caller)
    {
        var me = RequireCaller(caller);
        var audience = ToFlags(dto.Audiences);
        var count = audience == MailBroadcastAudience.None ? 0 : await AudienceUsers(audience, me).CountAsync();
        return new Response<int> { Status = 200, Message = "Destinataires comptés", Data = count };
    }

    public async Task<Response<MailBroadcastSent>> SendAsAdminAsync(
        AdminMailBroadcastCreate dto,
        CallerIdentity? caller
    )
    {
        var me = RequireCaller(caller);
        var audience = ToFlags(dto.Audiences);
        if (audience == MailBroadcastAudience.None)
        {
            throw new BusinessRuleException("Choisissez au moins un public");
        }
        var (subject, body) = Clean(dto.Subject, dto.Message);
        await EnsureQuotaAsync(me);

        var recipients = await AudienceUsers(audience, me).Select(u => u.Id).ToListAsync();

        return await RecordAndEnqueueAsync(me, MailBroadcastSenderKind.Manager, audience, subject, body, recipients);
    }

    // ---- Historique -------------------------------------------------------------------------

    /// <summary>Historique de tous les envois (réservé aux Managers).</summary>
    public async Task<Response<List<MailBroadcastSummary>>> SearchHistoryAsync(
        CustomTableState query,
        CallerIdentity? caller
    )
    {
        var me = RequireCaller(caller);

        var broadcasts = context.MailBroadcasts.AsNoTracking();
        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            broadcasts = broadcasts.Where(b =>
                EF.Functions.ILike(b.Subject, $"%{search}%")
                || EF.Functions.ILike(b.Sender!.FirstName + " " + b.Sender.LastName, $"%{search}%")
            );
        }

        var count = await broadcasts.CountAsync();
        var rows = await broadcasts
            .OrderByDescending(b => b.CreatedAt)
            .ThenBy(b => b.Id)
            .Skip(Math.Max(query.First, 0))
            .Take(query.Rows <= 0 ? DefaultRows : Math.Min(query.Rows, MaxRows))
            .Select(b => new
            {
                b.Id,
                b.SenderId,
                SenderName = b.Sender!.FirstName + " " + b.Sender.LastName,
                b.SenderKind,
                b.Audience,
                b.Subject,
                b.BodyHtml,
                b.RecipientCount,
                b.CreatedAt,
            })
            .ToListAsync();

        return new Response<List<MailBroadcastSummary>>
        {
            Status = 200,
            Message = "Envois récupérés avec succès",
            Data = rows.Select(b => new MailBroadcastSummary
                {
                    Id = b.Id,
                    SenderId = b.SenderId,
                    SenderName = b.SenderName.Trim(),
                    SenderKind = b.SenderKind,
                    Audiences = FromFlags(b.Audience),
                    Subject = b.Subject,
                    BodyHtml = b.BodyHtml,
                    RecipientCount = b.RecipientCount,
                    CreatedAt = b.CreatedAt,
                })
                .ToList(),
            Count = count,
        };
    }

    // ---- Envoi (job Hangfire) ---------------------------------------------------------------

    /// <summary>
    /// Met en file un mail par destinataire, chacun avec ses propres rejeux. Un destinataire
    /// archivé depuis l'enregistrement de l'envoi est sauté.
    /// </summary>
    [AutomaticRetry(Attempts = 0)]
    public async Task FanOutAsync(Guid broadcastId)
    {
        var broadcast = await context
            .MailBroadcasts.AsNoTracking()
            .Where(b => b.Id == broadcastId)
            .Select(b => new
            {
                b.SenderKind,
                b.Subject,
                b.BodyHtml,
                SenderName = b.Sender!.FirstName + " " + b.Sender.LastName,
                SenderEmail = b.Sender.Email,
            })
            .FirstOrDefaultAsync();
        if (broadcast is null)
        {
            logger.LogWarning("Envoi groupé {BroadcastId} introuvable.", broadcastId);
            return;
        }

        var recipients = await context
            .MailBroadcastRecipients.AsNoTracking()
            .Where(r => r.BroadcastId == broadcastId && r.User!.ArchivedAt == null && r.User.Email != null)
            .Select(r => new { r.User!.FirstName, r.User.Email })
            .ToListAsync();

        var replyTo = string.IsNullOrWhiteSpace(broadcast.SenderEmail) ? null : broadcast.SenderEmail;
        var websiteLink = EnvironmentVariables.API_FRONT_URL ?? "";

        foreach (var recipient in recipients)
        {
            await mailService.SendBroadcast(
                recipient.Email!,
                new BroadcastMailModel
                {
                    RecipientFirstName = recipient.FirstName,
                    SenderName = broadcast.SenderName.Trim(),
                    SenderKind = broadcast.SenderKind,
                    Subject = broadcast.Subject,
                    MessageHtml = broadcast.BodyHtml,
                    WebsiteLink = websiteLink,
                    AllowReply = replyTo is not null,
                },
                replyTo
            );
        }

        logger.LogInformation(
            "Envoi groupé {BroadcastId} : {Count} mails mis en file.",
            broadcastId,
            recipients.Count
        );
    }

    // ---- Interne ----------------------------------------------------------------------------

    private async Task<Response<MailBroadcastSent>> RecordAndEnqueueAsync(
        Guid senderId,
        MailBroadcastSenderKind kind,
        MailBroadcastAudience audience,
        string subject,
        string body,
        List<Guid> recipientIds
    )
    {
        if (recipientIds.Count == 0)
        {
            throw new BusinessRuleException("Aucun destinataire ne correspond à votre sélection");
        }

        var broadcast = new MailBroadcast
        {
            SenderId = senderId,
            SenderKind = kind,
            Audience = audience,
            Subject = subject,
            BodyHtml = body,
            RecipientCount = recipientIds.Count,
            Recipients = recipientIds.Select(id => new MailBroadcastRecipient { UserId = id }).ToList(),
        };
        context.MailBroadcasts.Add(broadcast);
        await context.SaveChangesAsync();

        BackgroundJob.Enqueue<MailBroadcastService>(s => s.FanOutAsync(broadcast.Id));

        return new Response<MailBroadcastSent>
        {
            Status = 201,
            Message = "Votre message est en cours d'envoi",
            Data = new MailBroadcastSent { Id = broadcast.Id, RecipientCount = recipientIds.Count },
        };
    }

    /// <summary>Comptes joignables : actifs, non archivés, avec une adresse, hors expéditeur.</summary>
    private IQueryable<UserApp> Reachable(Guid senderId) =>
        context.Users.Where(u =>
            u.Id != senderId
            && u.ArchivedAt == null
            && u.StatusId == HardCode.ACCOUNT_ACTIVE
            && u.Email != null
            && u.Email != ""
        );

    private IQueryable<UserApp> AudienceUsers(MailBroadcastAudience audience, Guid senderId)
    {
        var roleIds = new List<Guid>();
        if (audience.HasFlag(MailBroadcastAudience.Clients))
            roleIds.Add(HardCode.ROLE_CLIENT);
        if (audience.HasFlag(MailBroadcastAudience.Managers))
            roleIds.Add(HardCode.ROLE_MANAGER);

        return Reachable(senderId)
            .Where(u => context.UserRoles.Any(ur => ur.UserId == u.Id && roleIds.Contains(ur.RoleId)));
    }

    private (string Subject, string Body) Clean(string subject, string message)
    {
        var cleanSubject = subject.Trim();
        if (cleanSubject.Length == 0)
        {
            throw new BusinessRuleException("L'objet est obligatoire");
        }
        if (cleanSubject.Length > HardCode.MAIL_BROADCAST_SUBJECT_MAX_LENGTH)
        {
            throw new BusinessRuleException(
                $"L'objet ne peut pas dépasser {HardCode.MAIL_BROADCAST_SUBJECT_MAX_LENGTH} caractères"
            );
        }

        var body = sanitizer.Sanitize(message) ?? throw new BusinessRuleException("Le message est vide");
        if (sanitizer.TextLength(body) > HardCode.MAIL_BROADCAST_BODY_MAX_LENGTH)
        {
            throw new BusinessRuleException(
                $"Le message ne peut pas dépasser {HardCode.MAIL_BROADCAST_BODY_MAX_LENGTH} caractères"
            );
        }
        return (cleanSubject, body);
    }

    private async Task EnsureQuotaAsync(Guid senderId)
    {
        var since = DateTimeOffset.UtcNow.AddHours(-1);
        var recent = await context.MailBroadcasts.CountAsync(b => b.SenderId == senderId && b.CreatedAt >= since);
        if (recent >= HardCode.MAIL_BROADCAST_MAX_PER_HOUR)
        {
            throw new BusinessQuotaException(
                $"Vous avez atteint la limite de {HardCode.MAIL_BROADCAST_MAX_PER_HOUR} envois par heure. Réessayez plus tard."
            );
        }
    }

    private static MailBroadcastAudience ToFlags(IEnumerable<MailAudience>? audiences) =>
        (audiences ?? [])
            .Select(a => a switch
            {
                MailAudience.Clients => MailBroadcastAudience.Clients,
                MailAudience.Managers => MailBroadcastAudience.Managers,
                _ => MailBroadcastAudience.None,
            })
            .Aggregate(MailBroadcastAudience.None, (acc, flag) => acc | flag);

    private static List<MailAudience> FromFlags(MailBroadcastAudience audience)
    {
        var result = new List<MailAudience>();
        if (audience.HasFlag(MailBroadcastAudience.Clients))
            result.Add(MailAudience.Clients);
        if (audience.HasFlag(MailBroadcastAudience.Managers))
            result.Add(MailAudience.Managers);
        return result;
    }

    private static Guid RequireCaller(CallerIdentity? caller) =>
        caller?.UserId ?? throw new ResourceNotFoundException("Utilisateur non trouvé");
}
