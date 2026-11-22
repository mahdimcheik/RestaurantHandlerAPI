using RestaurantHandler.Contexts;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

/// <summary>
/// Envois groupés par e-mail (ADR-0023, US-E17-08 et US-E13-08). Un admin écrit à un ou plusieurs
/// publics ; un professeur écrit à ses élèves, c'est-à-dire ceux qui ont au moins une réservation
/// non rejetée et non annulée sur l'un de ses créneaux. L'envoi est historisé puis éclaté en un
/// mail par destinataire dans Hangfire : la requête ne paie pas le rendu ni le SMTP.
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

    private static readonly Guid[] EligibleReservationStatuses =
    [
        HardCode.RESERVATION_PENDING,
        HardCode.RESERVATION_ACCEPTED,
        HardCode.RESERVATION_DELIVERED,
        HardCode.RESERVATION_DONE,
        HardCode.RESERVATION_PAYABLE,
        HardCode.RESERVATION_DISPUTED,
    ];

    // ---- Professeur -------------------------------------------------------------------------

    public async Task<Response<List<MailingStudent>>> GetEligibleStudentsAsync(CallerIdentity? caller)
    {
        var me = RequireCaller(caller);

        var students = await EligibleStudentUsers(me)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ThenBy(u => u.Id)
            .Select(u => new MailingStudent
            {
                Id = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
            })
            .ToListAsync();

        return new Response<List<MailingStudent>>
        {
            Status = 200,
            Message = "Élèves récupérés avec succès",
            Data = students,
            Count = students.Count,
        };
    }

    public async Task<Response<MailBroadcastSent>> SendAsTeacherAsync(
        TeacherMailBroadcastCreate dto,
        CallerIdentity? caller
    )
    {
        var me = RequireCaller(caller);
        var (subject, body) = Clean(dto.Subject, dto.Message);
        await EnsureQuotaAsync(me);

        var eligible = await EligibleStudentUsers(me).Select(u => u.Id).ToListAsync();
        List<Guid> recipients;
        if (dto.StudentIds is { Count: > 0 })
        {
            var requested = dto.StudentIds.Distinct().ToList();
            if (requested.Except(eligible).Any())
            {
                throw new BusinessForbiddenException(
                    "Vous ne pouvez écrire qu'à des élèves ayant réservé au moins un cours avec vous"
                );
            }
            recipients = requested;
        }
        else
        {
            recipients = eligible;
        }

        return await RecordAndEnqueueAsync(
            me,
            MailBroadcastSenderKind.Teacher,
            MailBroadcastAudience.None,
            subject,
            body,
            recipients
        );
    }

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

        return await RecordAndEnqueueAsync(me, MailBroadcastSenderKind.Admin, audience, subject, body, recipients);
    }

    // ---- Historique -------------------------------------------------------------------------

    /// <summary>Tous les envois pour l'administration, les siens seulement pour un professeur.</summary>
    public async Task<Response<List<MailBroadcastSummary>>> SearchHistoryAsync(
        CustomTableState query,
        CallerIdentity? caller
    )
    {
        var me = RequireCaller(caller);

        var broadcasts = context.MailBroadcasts.AsNoTracking();
        if (!caller!.IsAdmin)
        {
            broadcasts = broadcasts.Where(b => b.SenderId == me);
        }

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

    private IQueryable<UserApp> EligibleStudentUsers(Guid teacherId) =>
        Reachable(teacherId)
            .Where(u =>
                context.Reservations.Any(r =>
                    r.StudentId == u.Id
                    && r.Slot!.TeacherId == teacherId
                    && EligibleReservationStatuses.Contains(r.StatusId)
                )
            );

    private IQueryable<UserApp> AudienceUsers(MailBroadcastAudience audience, Guid senderId)
    {
        var roleIds = new List<Guid>();
        if (audience.HasFlag(MailBroadcastAudience.Teachers))
            roleIds.Add(HardCode.ROLE_TEACHER);
        if (audience.HasFlag(MailBroadcastAudience.Students))
            roleIds.Add(HardCode.ROLE_STUDENT);
        if (audience.HasFlag(MailBroadcastAudience.Admins))
        {
            roleIds.Add(HardCode.ROLE_ADMIN);
            roleIds.Add(HardCode.ROLE_SUPER_ADMIN);
        }

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
                MailAudience.Teachers => MailBroadcastAudience.Teachers,
                MailAudience.Students => MailBroadcastAudience.Students,
                MailAudience.Admins => MailBroadcastAudience.Admins,
                _ => MailBroadcastAudience.None,
            })
            .Aggregate(MailBroadcastAudience.None, (acc, flag) => acc | flag);

    private static List<MailAudience> FromFlags(MailBroadcastAudience audience)
    {
        var result = new List<MailAudience>();
        if (audience.HasFlag(MailBroadcastAudience.Teachers))
            result.Add(MailAudience.Teachers);
        if (audience.HasFlag(MailBroadcastAudience.Students))
            result.Add(MailAudience.Students);
        if (audience.HasFlag(MailBroadcastAudience.Admins))
            result.Add(MailAudience.Admins);
        return result;
    }

    private static Guid RequireCaller(CallerIdentity? caller) =>
        caller?.UserId ?? throw new ResourceNotFoundException("Utilisateur non trouvé");
}
