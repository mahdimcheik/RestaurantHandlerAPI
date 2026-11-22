using RestaurantHandler.Contexts;
using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

public class TeacherProfileService
{
    private readonly MainContext _context;
    private readonly UserManager<UserApp> _userManager;
    private readonly InseeSireneService _sireneService;
    private readonly StorageService _storage;
    private readonly RichTextSanitizer _sanitizer;

    public TeacherProfileService(
        MainContext context,
        UserManager<UserApp> userManager,
        InseeSireneService sireneService,
        StorageService storage,
        RichTextSanitizer sanitizer
    )
    {
        _sanitizer = sanitizer;
        _context = context;
        _userManager = userManager;
        _sireneService = sireneService;
        _storage = storage;
    }

    public async Task<Response<UserDetails>> GetTeacherFullProfileAsync(CallerIdentity? User)
    {
        var user = CheckUser.GetUser(User, _context);
        if (user is null)
        {
            return new Response<UserDetails>
            {
                Status = 404,
                Message = "Profil enseignant non trouvé",
            };
        }
        var teacher = await _context
            .Users.Where(p => p.Id == user.Id)
            .Include(p => p.Gender)
            .Include(p => p.Teacher)
            .Include(p => p.Teacher)
            .FirstOrDefaultAsync();

        if (teacher == null)
        {
            return new Response<UserDetails>
            {
                Status = 404,
                Message = "Profil enseignant non trouvé",
            };
        }

        if (user.ImgUrl is not null)
        {
            user.ImgUrl = _storage.Url(user.ImgUrl);
        }

        var rolesDetailed = await CheckUser.GetRoles(user, _context, _userManager);

        return new Response<UserDetails>
        {
            Status = 200,
            Message = "Profil enseignant récupéré avec succès",
            Data = WithOwnPhoneNumber(new UserDetails(teacher, rolesDetailed), teacher.Teacher),
        };
    }

    public async Task<Response<UserDetails>> UpdateTeacherProfileAsync(
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

        var teacher = await _context
            .Users.Include(t => t.Teacher)
            .FirstOrDefaultAsync(t => t.Id == user.Id);

        if (teacher == null)
        {
            return new Response<UserDetails>
            {
                Status = 404,
                Message = "Profil enseignant non trouvé",
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
            .Include(u => u.Teacher)
            .FirstOrDefaultAsync(u => u.Id == user.Id);

        if (updatedUser.ImgUrl is not null)
        {
            updatedUser.ImgUrl = _storage.Url(updatedUser.ImgUrl);
        }

        return new Response<UserDetails>
        {
            Status = 200,
            Message = "Profil enseignant mis à jour avec succès",
            Data = WithOwnPhoneNumber(new UserDetails(updatedUser!, null), updatedUser!.Teacher),
        };
    }

    // Le numéro n'est jamais rempli par le constructeur de TeacherDetails (servi en anonyme) :
    // seules les réponses destinées au prof lui-même le portent.
    private static UserDetails WithOwnPhoneNumber(UserDetails details, Teacher? teacher)
    {
        if (details.Teacher is not null && teacher is not null)
        {
            details.Teacher.PhoneNumber = teacher.PhoneNumber;
            details.Teacher.ShowPhoneNumber = teacher.ShowPhoneNumber;
        }
        return details;
    }

    public async Task<Response<UserDetails>> SwitchToProfessionalAsync(
        TeacherSwitchToProfessional dto,
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

        var userWithTeacher = await _context
            .Users.Include(u => u.Teacher)
            .FirstOrDefaultAsync(u => u.Id == user.Id);

        if (userWithTeacher?.Teacher == null)
        {
            return new Response<UserDetails>
            {
                Status = 404,
                Message = "Profil enseignant non trouvé",
                Data = null,
            };
        }

        if (userWithTeacher.Teacher.IsProfessional)
        {
            return new Response<UserDetails>
            {
                Status = 400,
                Message = "\"Ce compte est déjà en mode professionnel\"",
                Data = null,
            };
        }

        // Vérifier le SIRET auprès de l'INSEE (même contrat qu'à l'inscription)
        var siretVerification = await _sireneService.VerifySiretAsync(dto.Siret);

        if (siretVerification.Status == SiretVerificationApiStatus.NotFound)
        {
            return new Response<UserDetails>
            {
                Status = 400,
                Message = "\"Le SIRET renseigné n'est pas reconnu par l'INSEE\"",
            };
        }

        if (siretVerification.Status == SiretVerificationApiStatus.Closed)
        {
            return new Response<UserDetails>
            {
                Status = 400,
                Message = "\"L'établissement associé à ce SIRET n'est plus actif\"",
            };
        }

        if (siretVerification.Status == SiretVerificationApiStatus.ServiceUnavailable)
        {
            return new Response<UserDetails>
            {
                Status = 503,
                Message = "\"Vérification du SIRET momentanément indisponible, veuillez réessayer plus tard\"",
            };
        }
        // Seul Active atteint ce point

        userWithTeacher.Teacher.Siret = dto.Siret;
        userWithTeacher.Teacher.ApplySiretVerification(siretVerification);

        // La dénomination INSEE fait foi pour un prof : elle écrase un titre éventuellement saisi.
        if (siretVerification.CompanyName is not null)
        {
            userWithTeacher.Title = siretVerification.CompanyName;
        }


        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        var updatedUser = await _context
            .Users.Include(p => p.Gender)
            .Include(p => p.Formations)
            .Include(u => u.Teacher)
            .FirstOrDefaultAsync(u => u.Id == user.Id);

        if (updatedUser.ImgUrl is not null)
        {
            updatedUser.ImgUrl = _storage.Url(updatedUser.ImgUrl);
        }

        return new Response<UserDetails>
        {
            Status = 200,
            Message = "Mode professionnel activé",
            Data = new UserDetails(updatedUser!, null),
        };
    }
}
