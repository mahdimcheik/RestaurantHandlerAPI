using System.Web;
using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

public class RegistrationService
{
    private readonly MainContext _context;
    private readonly UserManager<UserApp> _userManager;
    private readonly MailService _mailService;
    private readonly InseeSireneService _sireneService;
    private readonly RichTextSanitizer _sanitizer;

    public RegistrationService(
        MainContext context,
        UserManager<UserApp> userManager,
        MailService mailService,
        InseeSireneService sireneService,
        RichTextSanitizer sanitizer
    )
    {
        _context = context;
        _userManager = userManager;
        _mailService = mailService;
        _sireneService = sireneService;
        _sanitizer = sanitizer;
    }

    public async Task<Response<UserDetails>> Register(UserCreate newUserDTO)
    {
        // Vérifier le consentement (early check before any DB/network I/O)
        if (
            !newUserDTO.DataProcessingConsent
            || !newUserDTO.PrivacyPolicyConsent
            || !newUserDTO.TermsConsent
        )
        {
            return new Response<UserDetails>
            {
                Status = 400,
                Message =
                    "\"Le consentement est obligatoire pour acceder aux fonctionnalités de cette application\"",
            };
        }

        // Vérifier le SIRET auprès de l'INSEE (only for teachers, before any DB writes)
        SiretVerificationResult? siretVerification = null;
        if (newUserDTO.RoleId == HardCode.ROLE_TEACHER && newUserDTO.Teacher?.Siret is { } siret)
        {
            siretVerification = await _sireneService.VerifySiretAsync(siret);

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
            // Seul Active atteint ce point → l'inscription peut continuer
        }

        var transaction = _context.Database.BeginTransaction();
        try
        {

            bool isEmailAlreadyUsed = await IsEmailAlreadyUsedAsync(newUserDTO.Email);
            // Vérifier si l'adresse e-mail est déjà utilisée
            if (isEmailAlreadyUsed)
            {
                // Si l'adresse e-mail est déjà utilisée, mettre à jour la réponse et sauter vers l'étiquette UserAlreadyExisted
                return new Response<UserDetails>
                {
                    Status = 400,
                    Message = "\"L'email est déjà utilisé\"",
                };
            }
            // Créer un nouvel utilisateur en utilisant les données du modèle et la base de données contextuelle
            UserApp? newUser = new UserApp(newUserDTO);
            newUser.Description = _sanitizer.Sanitize(newUser.Description);
            newUser.AcceptTerms();
            newUser.CreatedAt = DateTime.UtcNow;

            // La dénomination INSEE fait foi pour un prof : elle écrase un titre éventuellement saisi.
            if (newUserDTO.RoleId == HardCode.ROLE_TEACHER && siretVerification?.CompanyName is not null)
            {
                newUser.Title = siretVerification.CompanyName;
            }

            // Obtenir la date actuelle
            DateTimeOffset date = DateTimeOffset.UtcNow;

            // Tenter de créer un nouvel utilisateur avec le gestionnaire d'utilisateurs
            IdentityResult result = await _userManager.CreateAsync(newUser, newUserDTO.Password);

            // Tenter d'ajouter l'utilisateur aux rôles spécifiés dans le modèle
            IdentityResult roleResult = await _userManager.AddToRolesAsync(
                user: newUser,
                roles: newUserDTO.RoleId == HardCode.ROLE_TEACHER ? ["Teacher"] : ["Student"]
            );

            newUser = await _context
                .Users.Where(u => u.Id == newUser.Id)
                .Include(u => u.Status)
                .Include(x => x.Gender)
                .FirstOrDefaultAsync();

            if (newUser is null)
            {
                await transaction.RollbackAsync();
                return new Response<UserDetails>
                {
                    Message = "Création échouée",
                    Status = 404,
                    Data = null,
                };
            }

            // Vérifier si la création de l'utilisateur a échoué
            if (!result.Succeeded)
            {
                // Si la création a échoué, ajouter les erreurs au modèle d'état pour retourner une réponse BadRequest
                var errors = Enumerable.Empty<string>();
                foreach (var error in result.Errors)
                {
                    errors.Append(error.Description);
                }

                // Retourner une réponse BadRequest avec le modèle d'état contenant les erreurs
                return new Response<UserDetails>
                {
                    Message = "Création échouée",
                    Status = 401,
                    Data = null,
                };
            }

            // Si tout s'est bien déroulé, enregistrer les changements dans le contexte de base de données
            await _context.SaveChangesAsync();

            // creer les profiles
            await CreateProfile(newUser, newUserDTO, siretVerification);
            await transaction.CommitAsync();

            try
            {
                await SendConfirmationLink(newUser);

                // Retourne une réponse avec le statut déterminé, l'identifiant de l'utilisateur, le message de réponse et le statut complet
                return new Response<UserDetails>
                {
                    Message = "Profil créé",
                    Status = 201,
                    Data = new UserDetails(newUser, null),
                };
            }
            catch (Exception e)
            {
                // En cas d'exception, afficher la trace et retourner une réponse avec le statut approprié
                Console.WriteLine(e);
                return new Response<UserDetails>
                {
                    Status = 200,
                    Message = "Le compte est créé mais  pas d'email de validation!!!",
                };
            }
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return new Response<UserDetails>
            {
                Message = "Création échouée",
                Status = 401,
                Data = null,
            };
        }
    }

    public async Task CreateProfile(UserApp newUser, UserCreate userCreate, SiretVerificationResult? siretVerification)
    {
        try
        {
            if (userCreate.RoleId == HardCode.ROLE_TEACHER && userCreate.Teacher is not null)
            {
                Teacher newTeacher = new Teacher(userCreate.Teacher, newUser.Id, siretVerification);
                await _context.Teachers.AddAsync(newTeacher);
                await TeacherVerificationHelper.CreateDefaultDocumentRequestsAsync(_context, newTeacher.Id);


                await _context.SaveChangesAsync();
            }
            else
            {
                Student newStudent = new Student { Id = newUser.Id, UserId = newUser.Id };
                await _context.Students.AddAsync(newStudent);
                await _context.SaveChangesAsync();
            }
        }
        catch
        {
            throw;
        }
    }

    /// <summary>
    /// Valide le lien d'activation et renvoie l'URL front vers laquelle rediriger : page de
    /// succès, ou page « lien expiré » qui propose d'en recevoir un nouveau.
    /// </summary>
    public async Task<string> EmailConfirmation(string userId, string confirmationToken)
    {
        var success = $"{EnvironmentVariables.API_FRONT_URL}/auth/email-confirmation-success";
        var failure = $"{EnvironmentVariables.API_FRONT_URL}/auth/email-confirmation-failed";

        var user = Guid.TryParse(userId, out _) ? await _userManager.FindByIdAsync(userId) : null;
        if (user is null || user.ArchivedAt is not null)
        {
            return failure;
        }

        // Un ancien lien (expiré ou déjà utilisé) d'un compte déjà activé n'est pas une erreur.
        if (user.EmailConfirmed)
        {
            return success;
        }

        var result = await _userManager.ConfirmEmailAsync(user, confirmationToken);
        if (!result.Succeeded)
        {
            return $"{failure}?userId={user.Id}";
        }

        // Seul un compte en attente devient actif : un compte suspendu le reste.
        if (user.StatusId == HardCode.ACCOUNT_PENDING)
        {
            user.StatusId = HardCode.ACCOUNT_ACTIVE;
            await _context.SaveChangesAsync();
        }

        return success;
    }

    public static readonly TimeSpan ConfirmationResendCooldown = TimeSpan.FromMinutes(2);

    public enum ResendConfirmationResult
    {
        Sent,
        NotApplicable,
        AlreadyConfirmed,
        TooSoon,
    }

    /// <summary>
    /// Renvoie un lien d'activation, au plus une fois toutes les
    /// <see cref="ConfirmationResendCooldown"/>.
    /// </summary>
    public async Task<ResendConfirmationResult> ResendConfirmation(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.ArchivedAt is not null || user.StatusId == HardCode.ACCOUNT_BANNED)
        {
            return ResendConfirmationResult.NotApplicable;
        }

        if (user.EmailConfirmed)
        {
            return ResendConfirmationResult.AlreadyConfirmed;
        }

        if (
            user.ConfirmationEmailSentAt is { } sentAt
            && DateTimeOffset.UtcNow - sentAt < ConfirmationResendCooldown
        )
        {
            return ResendConfirmationResult.TooSoon;
        }

        await SendConfirmationLink(user);
        return ResendConfirmationResult.Sent;
    }

    private async Task SendConfirmationLink(UserApp user)
    {
        var confirmationLink = await GenerateAccountConfirmationLink(user);
        await _mailService.SendConfirmAccount(user, confirmationLink);

        user.ConfirmationEmailSentAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();
    }

    private async Task<string> GenerateAccountConfirmationLink(UserApp user)
    {
        var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        confirmationToken = HttpUtility.UrlEncode(confirmationToken);

        return EnvironmentVariables.API_BACK_URL
            + "/auth/email-confirmation?userId="
            + user.Id
            + "&confirmationToken="
            + confirmationToken;
    }

    private async Task<bool> IsEmailAlreadyUsedAsync(string email)
    {
        var existingUser = await _userManager.FindByEmailAsync(email);
        return existingUser != null;
    }
}
