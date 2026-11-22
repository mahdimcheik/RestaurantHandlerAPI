using RestaurantHandler.Contexts;
using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

/// <summary>
/// Profils : lecture d'un profil par identifiant, mutualisée entre enseignants, élèves et
/// administration (<see cref="GetProfileByIdAsync"/>, chaque appelant conservant sa forme de
/// réponse via les options), et profil de l'élève.
/// </summary>
public class ProfileService
{
    private readonly MainContext _context;
    private readonly UserManager<UserApp> _userManager;
    private readonly StorageService _storage;

    private readonly RichTextSanitizer _sanitizer;

    public ProfileService(MainContext context, UserManager<UserApp> userManager, StorageService storage, RichTextSanitizer sanitizer)
    {
        _context = context;
        _userManager = userManager;
        _storage = storage;
        _sanitizer = sanitizer;
    }

    // ----------------------------------------------------------------------
    // Profil par identifiant (mutualisé entre les 3 contrôleurs)
    // ----------------------------------------------------------------------

    /// <summary>
    /// Récupère un profil utilisateur par identifiant. Les options permettent à
    /// chaque appelant (teacher / student / admin) de conserver exactement la forme
    /// de réponse qu'il produisait auparavant (includes, rôles, adresses minimales,
    /// filtrage des comptes archivés, messages).
    /// </summary>
    public async Task<Response<UserDetails>> GetProfileByIdAsync(
        Guid id,
        bool includeRoles = true,
        bool includeStatus = false,
        bool includeTeacher = false,
        bool includeStudent = false,
        bool includeFormations = false,
        bool onlyActive = false,
        string notFoundMessage = "Utilisateur non trouvé",
        string successMessage = "Utilisateur récupéré avec succès"
    )
    {
        var query = _context
            .Users.Include(u => u.Gender)
            .AsQueryable();

        if (includeStatus)
        {
            query = query.Include(u => u.Status);
        }
        if (includeTeacher)
        {
            query = query.Include(u => u.Teacher);
        }
        if (includeStudent)
        {
            query = query.Include(u => u.Student);
        }
        if (includeFormations)
        {
            query = query.Include(u => u.Formations);
        }

        var user = await query.FirstOrDefaultAsync(u =>
            u.Id == id && (!onlyActive || u.ArchivedAt == null)
        );

        if (user is null)
        {
            return new Response<UserDetails> { Status = 404, Message = notFoundMessage };
        }

        if (user.ImgUrl is not null)
        {
            user.ImgUrl = _storage.Url(user.ImgUrl);
        }

        var roles = includeRoles
            ? await CheckUser.GetRoles(user, _context, _userManager)
            : null;

        return new Response<UserDetails>
        {
            Status = 200,
            Message = successMessage,
            Data = new UserDetails(user, roles),
        };
    }

    /// <summary>
    /// Profil enseignant par identifiant.
    /// </summary>
    /// L'email n'est jamais servi ici (endpoint anonyme) : cf. <see cref="GetTeacherEmailAsync"/>.
    public async Task<Response<UserDetails>> GetTeacherProfileByUserIdAsync(Guid userId)
    {
        var response = await GetProfileByIdAsync(
            userId,
            includeRoles: true,
            includeTeacher: true,
            notFoundMessage: "Profil enseignant non trouvé",
            successMessage: "Profil enseignant récupéré avec succès"
        );

        if (response.Data is not null)
        {
            response.Data.Email = "";
        }
        return response;
    }

    /// <summary>
    /// Numéro du prof, seulement s'il a choisi de l'afficher. « Masqué » et « absent »
    /// renvoient le même 404 pour ne rien révéler.
    /// </summary>
    public async Task<Response<string>> GetTeacherPhoneAsync(Guid userId)
    {
        var phone = await _context
            .Teachers.Where(t =>
                t.UserId == userId
                && t.ShowPhoneNumber
                && t.PhoneNumber != null
                && t.User!.ArchivedAt == null
            )
            .Select(t => t.PhoneNumber)
            .FirstOrDefaultAsync();

        return phone is null
            ? new Response<string> { Status = 404, Message = "Numéro non disponible" }
            : new Response<string> { Status = 200, Message = "Numéro récupéré avec succès", Data = phone };
    }

    /// <summary>
    /// Email du prof, seulement s'il a choisi de l'afficher. « Masqué » et « absent »
    /// renvoient le même 404 pour ne rien révéler.
    /// </summary>
    public async Task<Response<string>> GetTeacherEmailAsync(Guid userId)
    {
        var email = await _context
            .Teachers.Where(t =>
                t.UserId == userId
                && t.ShowEmail
                && t.User!.Email != null
                && t.User.ArchivedAt == null
            )
            .Select(t => t.User!.Email)
            .FirstOrDefaultAsync();

        return email is null
            ? new Response<string> { Status = 404, Message = "Email non disponible" }
            : new Response<string> { Status = 200, Message = "Email récupéré avec succès", Data = email };
    }

    /// <summary>
    /// Profil élève par identifiant (forme historique de StudentService : sans rôles).
    /// </summary>
    public async Task<Response<UserDetails>> GetStudentByUserIdAsync(
        Guid userId,
        CallerIdentity caller
    )
    {
        if (!CanReadStudentProfile(userId, caller))
            return StudentProfileNotFound();

        return await GetProfileByIdAsync(
            userId,
            includeRoles: false,
            includeStudent: true,
            notFoundMessage: StudentProfileNotFoundMessage,
            successMessage: "Profil élève récupéré avec succès"
        );
    }

    private const string StudentProfileNotFoundMessage = "Profil élève non trouvé";

    private static Response<UserDetails> StudentProfileNotFound() =>
        new() { Status = 404, Message = StudentProfileNotFoundMessage };

    /// <summary>
    /// Le profil d'un élève est lisible par lui-même et par l'administration. Pour tout autre
    /// appelant, il n'existe pas.
    /// </summary>
    private static bool CanReadStudentProfile(Guid studentUserId, CallerIdentity caller) =>
        caller.IsAdmin || caller.UserId == studentUserId;


    // ----------------------------------------------------------------------
    // Élèves
    // ----------------------------------------------------------------------

    public async Task<Response<UserDetails>> GetStudentFullProfileAsync(CallerIdentity? principal)
    {
        var user = CheckUser.GetUser(principal, _context);
        if (user is null)
        {
            return new Response<UserDetails>
            {
                Status = 404,
                Message = "Profil enseignant non trouvé",
            };
        }
        var student = await _context
            .Users.Where(p => p.Id == user.Id)
            .Include(p => p.Gender)
            .Include(p => p.Student)
            .Include(p => p.UserRoles)
            .FirstOrDefaultAsync();

        if (student == null)
        {
            return new Response<UserDetails>
            {
                Status = 404,
                Message = "Profil enseignant non trouvé",
            };
        }
        // les roles
        var rolesDetailed = await CheckUser.GetRoles(user, _context, _userManager);

        if (user.ImgUrl is not null)
        {
            user.ImgUrl = _storage.Url(user.ImgUrl);
        }

        return new Response<UserDetails>
        {
            Status = 200,
            Message = "Profil enseignant récupéré avec succès",
            Data = new UserDetails(student, rolesDetailed),
        };
    }

    public async Task<Response<UserDetails>> GetStudentpublicProfileAsync(
        Guid userId,
        CallerIdentity caller
    )
    {
        if (!CanReadStudentProfile(userId, caller))
            return StudentProfileNotFound();

        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.Id == userId && u.ArchivedAt == null
        );
        if (user is null)
        {
            return new Response<UserDetails>
            {
                Status = 404,
                Message = "Profil enseignant non trouvé",
            };
        }
        var student = await _context
            .Users.Where(p => p.Id == user.Id)
            .Include(p => p.Gender)
            .Include(p => p.Student)
            .Include(p => p.UserRoles)
            .FirstOrDefaultAsync();

        if (student == null)
        {
            return new Response<UserDetails>
            {
                Status = 404,
                Message = "Profil enseignant non trouvé",
            };
        }
        // les roles
        var rolesDetailed = await CheckUser.GetRoles(user, _context, _userManager);

        if (user.ImgUrl is not null)
        {
            user.ImgUrl = _storage.Url(user.ImgUrl);
        }

        return new Response<UserDetails>
        {
            Status = 200,
            Message = "Profil enseignant récupéré avec succès",
            Data = new UserDetails(student, rolesDetailed),
        };
    }

    public async Task<Response<UserDetails>> UpdateStudentProfileAsync(
        UserUpdate userUpdate,
        CallerIdentity? userPrincipal
    )
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        var user = CheckUser.GetUser(userPrincipal, _context);
        if (user == null)
        {
            return new Response<UserDetails>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
                Data = null,
            };
        }

        var student = await _context
            .Users.Include(t => t.Student)
            .FirstOrDefaultAsync(t => t.Id == user.Id);

        if (student == null)
        {
            return new Response<UserDetails>
            {
                Status = 404,
                Message = "Profil élève non trouvé",
                Data = null,
            };
        }


        userUpdate.UpdateUser(user);
        // Texte enrichi affiché sur le profil public et sous les articles : nettoyé comme les autres (ADR-0014).
        user.Description = _sanitizer.Sanitize(user.Description);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        var updatedUser = await _context
            .Users.Include(p => p.Gender)
            .Include(p => p.Formations)
            .Include(u => u.Student)
            .FirstOrDefaultAsync(u => u.Id == user.Id);

        if (updatedUser.ImgUrl is not null)
        {
            updatedUser.ImgUrl = _storage.Url(updatedUser.ImgUrl);
        }

        return new Response<UserDetails>
        {
            Status = 200,
            Message = "Profil enseignant mis à jour avec succès",
            Data = new UserDetails(updatedUser!, null),
        };
    }
}
