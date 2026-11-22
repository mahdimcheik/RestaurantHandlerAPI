using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using RestaurantHandler.Models.Interfaces;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Models;

public class UserDetails
{
    public required Guid Id { get; set; }

    public required string Email { get; set; }

    public required DateTimeOffset DateOfBirth { get; set; }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? ImgUrl { get; set; }

    public StatusAccountDetails? Status { get; set; }
    public bool? EmailConfirmed { get; set; }
    public GenderDetails? Gender { get; set; }

    [Required]
    public required ICollection<RoleDetails>? Roles { get; set; }
    public required ICollection<FormationDetails> Formations { get; set; }

    public TeacherDetails? Teacher { get; set; }
    public StudentDetails? Student { get; set; }


    /// <summary>Faux tant que l'utilisateur n'a pas accepté la version courante des CGU.</summary>
    public bool TermsUpToDate { get; set; }

    /// <summary>
    /// Préférences visuelles enregistrées ; null si l'utilisateur n'en a jamais enregistré.
    /// Renseignées uniquement dans les réponses de session (<see cref="WithLayoutSettings"/>) :
    /// ce DTO sert aussi aux profils publics.
    /// </summary>
    public LayoutSettingsDetails? LayoutSettings { get; set; }


    public UserDetails() { }

    public UserDetails WithLayoutSettings(UserApp user)
    {
        LayoutSettings = user.LayoutSettings is not null ? new LayoutSettingsDetails(user.LayoutSettings) : null;
        return this;
    }

    [SetsRequiredMembers]
    public UserDetails(UserApp user, List<RoleDetails>? roles)
    {
        Id = user.Id;
        Email = user.Email ?? user.UserName ?? "";
        FirstName = user.FirstName;
        LastName = user.LastName;
        Title = user.Title;
        Description = user.Description;
        DateOfBirth = user.DateOfBirth;
        ImgUrl = user.ImgUrl;

        Roles = roles;
        Status = user.Status is not null ? new StatusAccountDetails(user.Status) : null;
        EmailConfirmed = user.EmailConfirmed;
        Gender = user.Gender is not null ? new GenderDetails(user.Gender) : null;
        Teacher = user.Teacher is not null ? new TeacherDetails(user.Teacher) : null;
        Student = user.Student is not null ? new StudentDetails(user.Student) : null;
        TermsUpToDate = user.TermsVersion == HardCode.TERMS_VERSION;

        Formations = user.Formations?.Select(f => new FormationDetails(f)).ToList() ?? [];
    }

    [SetsRequiredMembers]
    public UserDetails(UserApp user, bool forStudent)
    {
        Id = user.Id;
        Email = user.Email ?? user.UserName ?? "";
        FirstName = user.FirstName;
        LastName = user.LastName;
        Title = user.Title;
        Description = user.Description;
        DateOfBirth = user.DateOfBirth;
        ImgUrl = user.ImgUrl;

        Status = user.Status is not null ? new StatusAccountDetails(user.Status) : null;
        Gender = user.Gender is not null ? new GenderDetails(user.Gender) : null;
        Student = user.Student is not null ? new StudentDetails(user.Student) : null;
    }
}

/// <summary>
/// Utilisateur vu par l'administration : identique à <see cref="UserDetails"/>, mais le profil
/// enseignant est un <see cref="TeacherAdminDetails"/> (SIRET, dénomination INSEE).
/// Réservé aux endpoints [Authorize(Roles = "Admin,SuperAdmin")].
/// </summary>
public class UserAdminDetails
{
    public required Guid Id { get; set; }

    public required string Email { get; set; }

    public required DateTimeOffset DateOfBirth { get; set; }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? ImgUrl { get; set; }

    public StatusAccountDetails? Status { get; set; }
    public GenderDetails? Gender { get; set; }

    [Required]
    public required ICollection<RoleDetails>? Roles { get; set; }
    public required ICollection<FormationDetails> Formations { get; set; }

    public TeacherAdminDetails? Teacher { get; set; }
    public StudentDetails? Student { get; set; }


    /// <summary>Date d'inscription du compte.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    public UserAdminDetails() { }

    [SetsRequiredMembers]
    public UserAdminDetails(UserApp user, List<RoleDetails>? roles)
    {
        Id = user.Id;
        Email = user.Email ?? user.UserName ?? "";
        FirstName = user.FirstName;
        LastName = user.LastName;
        Title = user.Title;
        Description = user.Description;
        DateOfBirth = user.DateOfBirth;
        ImgUrl = user.ImgUrl;
        CreatedAt = user.CreatedAt;

        Roles = roles;
        Status = user.Status is not null ? new StatusAccountDetails(user.Status) : null;
        Gender = user.Gender is not null ? new GenderDetails(user.Gender) : null;
        Teacher = user.Teacher is not null ? new TeacherAdminDetails(user.Teacher) : null;
        Student = user.Student is not null ? new StudentDetails(user.Student) : null;

        Formations = user.Formations?.Select(f => new FormationDetails(f)).ToList() ?? [];
    }
}

public class UserMinimalDetails
{
    public required Guid Id { get; set; }

    public required string Email { get; set; }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    public string? ImgUrl { get; set; }

    public UserMinimalDetails() { }
}

public class UserLogin
{
    /// <summary>
    /// Adresse email de l'utilisateur (format email valide requis)
    /// </summary>
    /// <example>utilisateur@exemple.com</example>
    [Required(ErrorMessage = "L'email est requis")]
    [EmailAddress(ErrorMessage = "Format d'email invalide")]
    public required string Email { get; set; }

    /// <summary>
    /// Mot de passe (minimum 8 caractères avec majuscules, minuscules, chiffres)
    /// </summary>
    /// <example>MonMotDePasse123!</example>
    [Required(ErrorMessage = "Le mot de passe est requis")]
    [MinLength(8, ErrorMessage = "Le mot de passe doit contenir au moins 8 caractères")]
    public required string Password { get; set; }
}

public class ResendConfirmationRequest
{
    /// <summary>Identifiant présent dans le lien d'activation expiré.</summary>
    [Required]
    public required Guid UserId { get; set; }
}

public class GoogleLoginRequest
{
    /// <summary>
    /// ID token (JWT) émis par Google Identity Services et récupéré côté front dans le
    /// champ <c>credential</c> de la réponse au bouton « Se connecter avec Google ».
    /// L'API en vérifie la signature et l'audience ; le front n'envoie rien d'autre.
    /// </summary>
    [Required(ErrorMessage = "Le token Google est requis")]
    public required string IdToken { get; set; }

    /// <summary>
    /// Rôle du compte à créer (élève par défaut, ou professeur). Ignoré si un compte existe
    /// déjà pour ce compte Google ou cet email : la connexion ne change jamais de rôle.
    /// </summary>
    public Guid? RoleId { get; set; }

    /// <summary>Consentements de l'inscription, exigés seulement pour créer un compte.</summary>
    public bool DataProcessingConsent { get; set; }

    public bool PrivacyPolicyConsent { get; set; }

    public bool TermsConsent { get; set; }
}

public class ConfirmAccount
{
    public required string UserId { get; set; }
    public required string ConfirmationToken { get; set; }
}

public class UserCreate
{
    [Required]
    [MaxLength(64)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string LastName { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "Le titre ne peut pas dépasser 200 caractères")]
    public string? Title { get; set; }

    [StringLength(10000, ErrorMessage = "La description ne peut pas dépasser 10000 caractères")]
    public string? Description { get; set; }

    [Required]
    [EmailAddress]
    [NoPlusInEmail]
    public required string Email { get; set; }

    [Required]
    [DataType(DataType.Password)]
    public required string Password { get; set; }

    [Required]
    public required bool DataProcessingConsent { get; set; } = false;

    [Required]
    public required bool PrivacyPolicyConsent { get; set; } = false;

    /// <summary>Acceptation des CGU, exigée à l'inscription (voir HardCode.TERMS_VERSION).</summary>
    public bool TermsConsent { get; set; }

    [Required]
    public Guid RoleId { get; set; } = HardCode.ROLE_STUDENT;

    [Required]
    public required DateTimeOffset DateOfBirth { get; set; }

    [Required]
    public Guid GenderId { get; set; }

    public TeacherCreate? Teacher { get; set; }
    public StudentCreate? Student { get; set; }
}

public class PasswordReset
{
    [Required]
    public required string ResetToken { get; set; } = string.Empty;

    [Required]
    public required string Email { get; set; } = string.Empty;

    [Required]
    public required Guid Id { get; set; }
}

public class ForgotPassword
{
    [Required(ErrorMessage = "Email required")]
    [DataType(DataType.EmailAddress)]
    public required string Email { get; set; }
}

public class ChangePassword
{
    [Required]
    public required string OldPassword { get; set; }

    [Required]
    public required string NewPassword { get; set; }

    [Required]
    public required string NewPasswordConfirmation { get; set; }
}

public class PasswordRecovery
{
    [Required(ErrorMessage = "UserId required")]
    public required string UserId { get; set; }

    [Required(ErrorMessage = "ConfirmationToken required")]
    public required string ResetToken { get; set; }

    [Required(ErrorMessage = "Password required")]
    public required string Password { get; set; }

    [Required(ErrorMessage = "PasswordConfirmation required")]
    public required string PasswordConfirmation { get; set; }
}

public class Login
{
    [Required]
    public required string Token { get; set; } = null!;

    [Required]
    public required string RefreshToken { get; set; } = null!;

    [Required]
    public required UserDetails User { get; set; } = null!;

    /// <summary>
    /// Vrai quand le compte a été créé par une connexion Google : Google ne fournit ni date
    /// de naissance ni genre, ces champs portent donc une valeur par défaut et le front peut
    /// proposer de compléter le profil. Toujours faux après une inscription classique, qui
    /// exige ces deux champs.
    /// </summary>
    public bool ProfileIncomplete { get; set; }
}

public class UserUpdate
{
    [Required]
    [MaxLength(64)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string LastName { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "Le titre ne peut pas dépasser 200 caractères")]
    public string? Title { get; set; }

    [StringLength(10000, ErrorMessage = "La description ne peut pas dépasser 10000 caractères")]
    public string? Description { get; set; }

    [Required]
    public required DateTimeOffset DateOfBirth { get; set; }

    [Required]
    public Guid GenderId { get; set; }
    public TeacherUpdate? Teacher { get; set; }
    public StudentUpdate? Student { get; set; }

    public void UpdateUser(UserApp user)
    {
        user.FirstName = FirstName;
        user.LastName = LastName;
        user.Title = Title;
        user.Description = Description;
        user.DateOfBirth = DateOfBirth;
        user.GenderId = GenderId;
        if (user.Teacher is not null && Teacher is not null)
        {
            Teacher.UpdateTeacher(user.Teacher);
        }

        if (user.Student is not null && Student is not null)
        {
            Student.UpdateStudent(user.Student);
        }
    }
}

public class ContactTeacher
{
    [Required]
    public required Guid TeacherId { get; set; }

    [Required]
    [StringLength(200, ErrorMessage = "Le sujet ne peut pas dépasser 200 caractères")]
    public required string Subject { get; set; }

    [Required]
    // [StringLength(5000, ErrorMessage = "Le message ne peut pas dépasser 5000 caractères")]
    public required string Message { get; set; }
}

public class UserInfosWithtoken
{
    [Required]
    public required string Token { get; set; }

    [Required]
    public required UserDetails User { get; set; }
}
