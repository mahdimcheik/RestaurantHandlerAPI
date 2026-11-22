using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using RestaurantHandler.Services;
using MimeKit;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace RestaurantHandler.Mcp;

/// <summary>
/// Envoi anonyme d'un message à un prof (ADR-0024). L'expéditeur n'a pas de compte : nom et email
/// sont déclaratifs, le mail le dit au prof et porte un Reply-To vers l'expéditeur.
/// </summary>
[McpServerToolType]
public sealed class TeacherContactTools
{
    public const string ContactTeacherName = "contact_teacher";
    public const string ContactTeacherDescription =
        "Envoie un message par email à un professeur BonProf, désigné par son identifiant (fast_search), "
        + "son nom ou son adresse email. Le professeur répond directement à l'adresse de l'expéditeur. "
        + "Faites relire et confirmer le message par l'utilisateur avant l'envoi. Limité à "
        + "5 envois par heure. "
        + "Sends an email message to a BonProf teacher identified by id, name or email; the teacher "
        + "replies to the sender's address. Ask the user to confirm the message before sending.";

    public const int SenderNameMaxLength = 100;
    public const int SubjectMaxLength = 200;
    public const int MessageMaxLength = 3000;

    public const string EmailAcceptedMessage =
        "Si un professeur BonProf correspond à cette adresse, il a reçu votre message et vous répondra "
        + "directement. If a BonProf teacher matches this address, they received your message.";

    private static readonly EmailAddressAttribute EmailValidator = new();

    [McpServerTool(
        Name = ContactTeacherName,
        Title = "Contacter un professeur",
        ReadOnly = false,
        Idempotent = false,
        Destructive = false,
        OpenWorld = true,
        UseStructuredContent = true
    )]
    [Description(ContactTeacherDescription)]
    public static async Task<ContactTeacherResponse> ContactTeacher(
        TeacherContactService contactService,
        McpContactLimiter limiter,
        IHttpContextAccessor httpContextAccessor,
        ILogger<TeacherContactTools> logger,
        [Description("Nom et prénom de l'expéditeur. Sender's full name.")] string senderName,
        [Description("Adresse email de l'expéditeur, où le prof répondra. Sender's email address.")]
            string senderEmail,
        [Description("Objet du message (200 caractères au plus). Subject.")] string subject,
        [Description("Message en texte brut (3000 caractères au plus). Plain-text message.")] string message,
        [Description("Identifiant du prof renvoyé par fast_search (prioritaire). Teacher id.")]
            Guid? teacherId = null,
        [Description("Nom du prof, « Prénom Nom » ou un seul des deux. Teacher name.")]
            string? teacherName = null,
        [Description("Adresse email du prof, si l'utilisateur la connaît. Teacher email.")]
            string? teacherEmail = null
    )
    {
        var request = Validate(teacherId, teacherName, teacherEmail, senderName, senderEmail, subject, message);

        // Avant la résolution : le quota ne doit rien révéler de l'existence d'une adresse.
        var ip = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (!limiter.TryAcquire(ip))
        {
            throw new McpException(
                $"Limite de {McpContactLimiter.PermitsPerHour} messages par heure atteinte, réessayez plus tard. "
                    + "Hourly message limit reached, try again later."
            );
        }

        var outcome = await contactService.SendExternalMessageAsync(request);
        logger.LogInformation(
            "MCP contact_teacher depuis {Ip} : {Status}, prof {TeacherId}",
            ip,
            outcome.Status,
            outcome.TeacherId
        );

        return outcome.Status switch
        {
            ExternalContactStatus.Sent => new ContactTeacherResponse(
                "sent",
                "Message envoyé au professeur. Message sent to the teacher.",
                outcome.TeacherId,
                []
            ),
            ExternalContactStatus.AcceptedByEmail => new ContactTeacherResponse(
                "accepted",
                EmailAcceptedMessage,
                null,
                []
            ),
            ExternalContactStatus.Ambiguous => new ContactTeacherResponse(
                "ambiguous",
                "Plusieurs professeurs portent ce nom : aucun message n'a été envoyé. Demandez à "
                    + "l'utilisateur lequel contacter, puis rappelez l'outil avec son teacherId. "
                    + "Several teachers match this name; nothing was sent. Retry with teacherId.",
                null,
                outcome
                    .Candidates.Select(c => new ContactTeacherCandidate(
                        c.Id,
                        c.FirstName,
                        c.LastName,
                        c.Courses,
                        McpPublicLinks.ProfileUrl(c.Id)
                    ))
                    .ToList()
            ),
            _ => throw new McpException(
                "Aucun professeur BonProf ne correspond. Utilisez fast_search pour le trouver. "
                    + "No matching BonProf teacher; use fast_search to find one."
            ),
        };
    }

    private static ExternalContactRequest Validate(
        Guid? teacherId,
        string? teacherName,
        string? teacherEmail,
        string senderName,
        string senderEmail,
        string subject,
        string message
    )
    {
        teacherName = McpPublicLinks.Blank(teacherName);
        teacherEmail = McpPublicLinks.Blank(teacherEmail);
        var targets = (teacherId is null ? 0 : 1) + (teacherName is null ? 0 : 1) + (teacherEmail is null ? 0 : 1);
        if (targets != 1)
            throw new McpException("Indiquez exactement un de teacherId, teacherName ou teacherEmail.");

        var name = SingleLine(senderName);
        if (name.Length == 0 || name.Length > SenderNameMaxLength)
            throw new McpException($"senderName est requis ({SenderNameMaxLength} caractères au plus).");

        var email = senderEmail?.Trim() ?? "";
        // DeliverAsync analyse le Reply-To avec MimeKit : une adresse qu'il refuse ferait échouer
        // l'envoi en boucle dans Hangfire.
        if (!EmailValidator.IsValid(email) || !MailboxAddress.TryParse(email, out _))
            throw new McpException("senderEmail n'est pas une adresse email valide.");

        if (teacherEmail is not null && !EmailValidator.IsValid(teacherEmail))
            throw new McpException("teacherEmail n'est pas une adresse email valide.");

        var cleanSubject = SingleLine(subject);
        if (cleanSubject.Length == 0 || cleanSubject.Length > SubjectMaxLength)
            throw new McpException($"subject est requis ({SubjectMaxLength} caractères au plus).");

        var cleanMessage = message?.Trim() ?? "";
        if (cleanMessage.Length == 0 || cleanMessage.Length > MessageMaxLength)
            throw new McpException($"message est requis ({MessageMaxLength} caractères au plus).");

        return new ExternalContactRequest(
            teacherId,
            teacherName,
            teacherEmail,
            name,
            email,
            cleanSubject,
            cleanMessage
        );
    }

    /// <summary>Espaces et retours à la ligne fusionnés : ces valeurs finissent dans un en-tête de mail.</summary>
    private static string SingleLine(string? value) =>
        string.Join(' ', (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}

public sealed record ContactTeacherResponse(
    [property: Description("sent, accepted (adresse email : envoyé si elle correspond à un prof) ou ambiguous.")]
        string Status,
    string Message,
    [property: Description("Prof destinataire ; absent quand le prof a été désigné par email.")] Guid? TeacherId,
    [property: Description("Profs correspondant au nom quand il est ambigu.")]
        List<ContactTeacherCandidate> Candidates
);

public sealed record ContactTeacherCandidate(
    Guid Id,
    string FirstName,
    string LastName,
    List<string> Courses,
    string ProfileUrl
);
