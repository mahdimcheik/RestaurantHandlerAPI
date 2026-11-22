using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

public class TeacherContactService
{
    private readonly MainContext _context;
    private readonly MailService _mailService;

    public TeacherContactService(MainContext context, MailService mailService)
    {
        _context = context;
        _mailService = mailService;
    }

    /// <summary>
    /// Envoie un message d'un élève vers un enseignant par email. L'élève est
    /// identifié via ses claims ; l'enseignant est résolu par son identifiant.
    /// Le mail part de l'adresse no-reply de la plateforme, l'email de l'élève
    /// étant rappelé dans le corps pour permettre une réponse.
    /// </summary>
    public async Task<Response<object>> SendMessageToTeacherAsync(
        ContactTeacher dto,
        CallerIdentity? principal
    )
    {
        var student = CheckUser.GetUser(principal, _context);
        if (student is null)
        {
            return new Response<object>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
            };
        }

        var teacher = await _context.Users.FirstOrDefaultAsync(u =>
            u.Id == dto.TeacherId && u.Teacher != null && u.ArchivedAt == null
        );

        if (teacher is null || string.IsNullOrWhiteSpace(teacher.Email))
        {
            return new Response<object> { Status = 404, Message = "Enseignant non trouvé" };
        }

        // dto.Message est du HTML volontaire (éditeur riche côté client) : le template
        // l'insère tel quel ; le nom et l'email de l'élève sont encodés par Razor.
        await _mailService.SendTeacherContact(teacher, student, dto.Subject, dto.Message);

        return new Response<object> { Status = 200, Message = "Message envoyé avec succès" };
    }

    private const int MaxNameCandidates = 10;

    /// <summary>
    /// Message d'un expéditeur sans compte, transmis par un assistant IA (outil MCP
    /// <c>contact_teacher</c>, ADR-0024). Le prof est désigné par son id, son nom ou son email,
    /// toujours parmi les profs actifs. Par email, l'issue est la même que l'adresse appartienne
    /// ou non à un prof : un appelant anonyme ne doit pas pouvoir s'en servir pour tester une
    /// adresse. Le nom ne compare que prénom et nom, jamais l'email.
    /// </summary>
    public async Task<ExternalContactOutcome> SendExternalMessageAsync(ExternalContactRequest request)
    {
        var active = _context.Users.Where(u =>
            u.Teacher != null && u.ArchivedAt == null && u.StatusId == HardCode.ACCOUNT_ACTIVE
        );

        UserApp? teacher;
        if (request.TeacherId is { } id)
        {
            teacher = await active.FirstOrDefaultAsync(u => u.Id == id);
            if (teacher is null)
                return ExternalContactOutcome.NotFound();
        }
        else if (request.TeacherEmail is { } email)
        {
            var normalized = email.Trim().ToUpperInvariant();
            teacher = await active.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized);
            if (teacher is not null)
                await SendExternalAsync(teacher, request);
            return ExternalContactOutcome.AcceptedByEmail(teacher?.Id);
        }
        else
        {
            var matches = await MatchByName(active, request.TeacherName ?? "")
                .AsNoTracking()
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .Take(MaxNameCandidates + 1)
                .Select(u => new ExternalContactCandidate(
                    u.Id,
                    u.FirstName,
                    u.LastName,
                    u.Teacher!.Cursuses.Where(c => c.ArchivedAt == null).Select(c => c.Name).ToList()
                ))
                .ToListAsync();

            if (matches.Count == 0)
                return ExternalContactOutcome.NotFound();
            if (matches.Count > 1)
                return ExternalContactOutcome.Ambiguous(matches.Take(MaxNameCandidates).ToList());

            teacher = await active.FirstAsync(u => u.Id == matches[0].Id);
        }

        await SendExternalAsync(teacher, request);
        return ExternalContactOutcome.Sent(teacher.Id);
    }

    /// <summary>
    /// « Prénom Nom » ou « Nom Prénom » exact, ou un seul mot égal au prénom ou au nom, sans
    /// tenir compte de la casse.
    /// </summary>
    private static IQueryable<UserApp> MatchByName(IQueryable<UserApp> users, string name)
    {
        var words = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return users.Where(_ => false);

        var full = string.Join(' ', words).ToLower();
        if (words.Length == 1)
            return users.Where(u => u.FirstName.ToLower() == full || u.LastName.ToLower() == full);

        return users.Where(u =>
            (u.FirstName + " " + u.LastName).ToLower() == full
            || (u.LastName + " " + u.FirstName).ToLower() == full
        );
    }

    private Task SendExternalAsync(UserApp teacher, ExternalContactRequest request) =>
        _mailService.SendExternalTeacherContact(
            new ExternalTeacherContactModel(
                teacher,
                request.SenderName,
                request.SenderEmail,
                request.Subject,
                request.Message,
                EnvironmentVariables.API_FRONT_URL ?? ""
            )
        );
}

/// <summary>Demande déjà validée : exactement un de TeacherId / TeacherName / TeacherEmail.</summary>
public sealed record ExternalContactRequest(
    Guid? TeacherId,
    string? TeacherName,
    string? TeacherEmail,
    string SenderName,
    string SenderEmail,
    string Subject,
    string Message
);

public sealed record ExternalContactCandidate(
    Guid Id,
    string FirstName,
    string LastName,
    List<string> Courses
);

public enum ExternalContactStatus
{
    Sent,
    AcceptedByEmail,
    Ambiguous,
    NotFound,
}

/// <param name="TeacherId">
/// Prof destinataire, pour la journalisation uniquement : ne jamais le renvoyer à l'appelant
/// quand <see cref="Status"/> vaut <see cref="ExternalContactStatus.AcceptedByEmail"/>.
/// </param>
public sealed record ExternalContactOutcome(
    ExternalContactStatus Status,
    Guid? TeacherId,
    List<ExternalContactCandidate> Candidates
)
{
    public static ExternalContactOutcome Sent(Guid teacherId) => new(ExternalContactStatus.Sent, teacherId, []);

    public static ExternalContactOutcome AcceptedByEmail(Guid? teacherId) =>
        new(ExternalContactStatus.AcceptedByEmail, teacherId, []);

    public static ExternalContactOutcome Ambiguous(List<ExternalContactCandidate> candidates) =>
        new(ExternalContactStatus.Ambiguous, null, candidates);

    public static ExternalContactOutcome NotFound() => new(ExternalContactStatus.NotFound, null, []);
}
