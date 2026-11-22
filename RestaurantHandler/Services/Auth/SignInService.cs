using RestaurantHandler.Contexts;
using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

public class SignInService
{
    private readonly MainContext _context;
    private readonly UserManager<UserApp> _userManager;
    private readonly GoogleAuthService _googleAuthService;
    private readonly SignInManager<UserApp> _signInManager;
    private readonly TokenService _tokenService;
    private readonly StorageService _storage;
    private readonly MaintenanceService _maintenanceService;

    private const string InvalidCredentialsMessage = "Identifiants invalides";

    public SignInService(
        MainContext context,
        UserManager<UserApp> userManager,
        GoogleAuthService googleAuthService,
        SignInManager<UserApp> signInManager,
        TokenService tokenService,
        StorageService storage,
        MaintenanceService maintenanceService
    )
    {
        _context = context;
        _userManager = userManager;
        _googleAuthService = googleAuthService;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _storage = storage;
        _maintenanceService = maintenanceService;
    }

    public async Task<Response<Login>> UpdateRefreshToken(string refreshToken)
    {
        var refreshTokenDB = await _context
            .RefreshTokens.Where(x =>
                x.Token == refreshToken && x.ExpirationDate > DateTimeOffset.UtcNow
            )
            .FirstOrDefaultAsync();

        if (refreshTokenDB is null)
        {
            return new Response<Login> { Message = "Token expiré ou non valide", Status = 401 };
        }

        var user = await _context
            .Users.Where(u => u.Id == refreshTokenDB.UserId)
            .Include(p => p.Gender)
            .Include(u => u.Teacher)
            .Include(u => u.Student)
            .FirstOrDefaultAsync();

        // Un compte banni ou désactivé depuis la connexion perd sa session au renouvellement.
        if (user is null || RefuseSignIn(user) is not null)
        {
            _context.RefreshTokens.Remove(refreshTokenDB);
            await _context.SaveChangesAsync();
            return new Response<Login> { Message = "Token expiré ou non valide", Status = 401 };
        }

        var userRoles = await _userManager.GetRolesAsync(user);
        var roles = _context.Roles.ToList();

        if (user.ImgUrl is not null)
        {
            user.ImgUrl = _storage.Url(user.ImgUrl);
        }

        var rolesDetailed = roles
            .Where(r => userRoles.Contains(r.Name ?? string.Empty))
            .Select(r => new RoleDetails(r))
            .ToList();

        return new Response<Login>
        {
            Message = "Autorisation renouvelée",
            Data = new Login
            {
                User = new UserDetails(user, rolesDetailed).WithLayoutSettings(user),
                Token = await _tokenService.GenerateAccessTokenAsync(user),
                RefreshToken = refreshToken,
            },
            Status = 200,
        };
    }

    /// <summary>
    /// Connexion par mot de passe. Adresse inconnue et mot de passe faux donnent la même réponse,
    /// et chaque échec compte pour le verrouillage (5 essais, cf. Program.ConfigureIdentity).
    /// </summary>
    public async Task<Response<Login>> Login(UserLogin model)
    {
        var invalid = new Response<Login> { Message = InvalidCredentialsMessage, Status = 401 };

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            return invalid;
        }

        var check = await _signInManager.CheckPasswordSignInAsync(
            user,
            model.Password,
            lockoutOnFailure: true
        );

        if (check.IsLockedOut)
        {
            return new Response<Login>
            {
                Message = "Trop de tentatives : compte verrouillé quelques minutes",
                Status = 423,
            };
        }

        if (!check.Succeeded)
        {
            return invalid;
        }

        var refusal = RefuseSignIn(user);
        if (refusal is not null)
        {
            return refusal;
        }

        // Pendant la maintenance, seul le SuperAdmin se connecte (pour la désactiver).
        var maintenance = await _maintenanceService.GetStatusAsync();
        if (
            maintenance.Enabled
            && !await _userManager.IsInRoleAsync(user, nameof(RoleEnum.SuperAdmin))
        )
        {
            return new Response<Login>
            {
                Message = MaintenanceService.DisplayMessage(maintenance),
                Status = 503,
            };
        }

        var fullUser = await _context
            .Users.Where(u => u.Id == user.Id)
            .Include(p => p.Gender)
            .Include(u => u.Teacher)
            .Include(u => u.Student)
            .FirstAsync();

        return await _tokenService.IssueSessionAsync(fullUser);
    }

    /// <summary>
    /// Garde commune à toutes les voies qui émettent ou prolongent une session : connexion par
    /// mot de passe, connexion Google et renouvellement. Renvoie <c>null</c> si le compte peut
    /// se connecter.
    /// </summary>
    private static Response<Login>? RefuseSignIn(UserApp user)
    {
        if (user.ArchivedAt is not null)
        {
            return new Response<Login> { Message = InvalidCredentialsMessage, Status = 401 };
        }

        if (user.StatusId == HardCode.ACCOUNT_BANNED)
        {
            return new Response<Login> { Message = "Ce compte est suspendu", Status = 403 };
        }

        // Un compte non activé peut se connecter : l'activation ne conditionne que la visibilité
        // d'un professeur.
        return null;
    }

    /// <summary>
    /// Connexion « Se connecter avec Google ».
    ///
    /// Le front envoie l'ID token obtenu de Google Identity Services ; on le valide
    /// (<see cref="GoogleAuthService.ValidateAsync"/>) puis on résout le compte dans cet
    /// ordre : login externe déjà enregistré → compte existant portant le même email
    /// (rattachement automatique, l'email étant attesté vérifié par Google) → création d'un
    /// nouveau compte élève ou professeur selon <see cref="GoogleLoginRequest.RoleId"/>.
    ///
    /// La réponse est le même <see cref="Login"/> que <see cref="Login"/> : le front réutilise
    /// son handler de connexion tel quel.
    /// </summary>
    public async Task<Response<Login>> GoogleLogin(GoogleLoginRequest model)
    {
        // Seuls les rôles publics de l'inscription sont acceptés : le paramètre ne doit pas
        // permettre de créer un compte d'administration.
        if (
            model.RoleId is { } roleId
            && roleId != HardCode.ROLE_STUDENT
            && roleId != HardCode.ROLE_TEACHER
        )
        {
            return new Response<Login> { Message = "Rôle non autorisé", Status = 400 };
        }

        var payload = await _googleAuthService.ValidateAsync(model.IdToken);
        if (payload is null)
        {
            return new Response<Login> { Message = "Connexion Google échouée", Status = 401 };
        }

        // 1. Compte déjà lié à ce compte Google (le sujet Google est l'identifiant stable,
        //    contrairement à l'email qui peut changer).
        var user = await _userManager.FindByLoginAsync(
            GoogleAuthService.LoginProvider,
            payload.Subject
        );

        // 2. Sinon, compte existant portant le même email : candidat au rattachement.
        var needsLink = false;
        if (user is null)
        {
            user = await _userManager.FindByEmailAsync(payload.Email);
            needsLink = user is not null;
        }

        // Le compte suspendu est écarté AVANT toute écriture : sinon le rattachement
        // ci-dessous rouvrirait l'accès à un utilisateur banni.
        if (
            user is not null
            && (user.StatusId == HardCode.ACCOUNT_BANNED || user.ArchivedAt is not null)
        )
        {
            return RefuseSignIn(user)!;
        }

        if (needsLink && user is not null)
        {
            var linkResult = await _userManager.AddLoginAsync(
                user,
                new UserLoginInfo(
                    GoogleAuthService.LoginProvider,
                    payload.Subject,
                    GoogleAuthService.LoginProvider
                )
            );

            if (!linkResult.Succeeded)
            {
                return new Response<Login>
                {
                    Message = "Impossible de lier ce compte Google",
                    Status = 400,
                };
            }

            // Google atteste l'email : un compte resté en attente de confirmation est activé,
            // l'utilisateur n'a plus besoin du mail de validation. Seul PENDING est promu —
            // aucun autre statut n'est touché ici.
            if (!user.EmailConfirmed || user.StatusId == HardCode.ACCOUNT_PENDING)
            {
                user.EmailConfirmed = true;

                if (user.StatusId == HardCode.ACCOUNT_PENDING)
                {
                    user.StatusId = HardCode.ACCOUNT_ACTIVE;
                }

                await _userManager.UpdateAsync(user);
            }
        }

        // 3. Sinon, création d'un nouveau compte, avec le rôle demandé. Comme pour
        //    l'inscription par email, elle exige les consentements et l'acceptation des CGU.
        if (user is null)
        {
            if (
                !model.DataProcessingConsent
                || !model.PrivacyPolicyConsent
                || !model.TermsConsent
            )
            {
                return new Response<Login>
                {
                    Message =
                        "Aucun compte n'est associé à ce compte Google : inscrivez-vous en acceptant les conditions générales d'utilisation, la politique de confidentialité et le traitement de vos données",
                    Status = 400,
                };
            }

            if (!NoPlusInEmailAttribute.IsAllowed(payload.Email))
            {
                return new Response<Login>
                {
                    Message = NoPlusInEmailAttribute.DefaultErrorMessage,
                    Status = 400,
                };
            }

            var creation = await CreateGoogleUserAsync(
                payload,
                asTeacher: model.RoleId == HardCode.ROLE_TEACHER
            );
            if (creation.user is null)
            {
                return new Response<Login> { Message = creation.error, Status = 400 };
            }

            user = creation.user;
        }

        // Recharger avec les navigations attendues par UserDetails (comme le fait Login).
        var fullUser = await _context
            .Users.Where(u => u.Id == user.Id)
            .Include(u => u.Status)
            .Include(u => u.Gender)
            .Include(u => u.Teacher)
            .Include(u => u.Student)
            .FirstOrDefaultAsync();

        if (fullUser is null)
        {
            return new Response<Login> { Message = "Connexion Google échouée", Status = 401 };
        }

        var refusal = RefuseSignIn(fullUser);
        if (refusal is not null)
        {
            return refusal;
        }

        return await _tokenService.IssueSessionAsync(fullUser);
    }

    /// <summary>
    /// Crée un compte à partir d'un payload Google validé : utilisateur sans mot de passe,
    /// email déjà confirmé, login externe, et rôle + profil élève (<see cref="Student"/>) ou
    /// professeur (<see cref="Teacher"/> sans SIRET, avec ses demandes de documents) — le
    /// pendant de <see cref="RegistrationService.Register"/> +
    /// <see cref="RegistrationService.CreateProfile"/> pour cette voie.
    ///
    /// <para>
    /// Google ne fournit ni date de naissance ni genre, tous deux non-nullables sur
    /// <see cref="UserApp"/> : on pose des valeurs par défaut et la connexion renvoie
    /// <c>ProfileIncomplete</c> pour que le front propose de compléter le profil.
    /// </para>
    /// </summary>
    private async Task<(UserApp? user, string error)> CreateGoogleUserAsync(
        GoogleJsonWebSignature.Payload payload,
        bool asTeacher
    )
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var newUser = new UserApp
            {
                UserName = payload.Email,
                Email = payload.Email,
                FirstName = payload.GivenName ?? string.Empty,
                LastName = payload.FamilyName ?? string.Empty,
                DateOfBirth = default,
                GenderId = HardCode.GENDER_OTHER,
                DataProcessingConsent = true, // vérifiés par GoogleLogin avant la création
                PrivacyPolicyConsent = true,
                TermsAcceptedAt = DateTimeOffset.UtcNow,
                TermsVersion = HardCode.TERMS_VERSION,
                EmailConfirmed = true,
                StatusId = HardCode.ACCOUNT_ACTIVE,
                CreatedAt = DateTime.UtcNow,
            };

            // Sans mot de passe : PasswordHash reste null, le compte n'est accessible que par
            // Google (l'utilisateur peut toujours passer par « mot de passe oublié »).
            var result = await _userManager.CreateAsync(newUser);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync();
                return (null, "Création échouée");
            }

            var roleResult = await _userManager.AddToRolesAsync(
                newUser,
                [asTeacher ? "Teacher" : "Student"]
            );
            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return (null, "Création échouée");
            }

            var loginResult = await _userManager.AddLoginAsync(
                newUser,
                new UserLoginInfo(
                    GoogleAuthService.LoginProvider,
                    payload.Subject,
                    GoogleAuthService.LoginProvider
                )
            );
            if (!loginResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return (null, "Création échouée");
            }

            if (asTeacher)
            {
                var teacher = new Teacher(new TeacherCreate(), newUser.Id, null);
                await _context.Teachers.AddAsync(teacher);
                await TeacherVerificationHelper.CreateDefaultDocumentRequestsAsync(
                    _context,
                    teacher.Id
                );
            }
            else
            {
                await _context.Students.AddAsync(
                    new Student { Id = newUser.Id, UserId = newUser.Id }
                );
            }
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            // Avatar en best-effort, hors transaction : un échec du stockage ne doit pas coûter le compte.
            var avatarKey = await _googleAuthService.TryFetchAvatarAsync(payload.Picture);
            if (avatarKey is not null)
            {
                newUser.ImgUrl = avatarKey;
                await _context.SaveChangesAsync();
            }

            return (newUser, string.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            await transaction.RollbackAsync();
            return (null, "Création échouée");
        }
    }
}
