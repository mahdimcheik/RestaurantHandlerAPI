using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using RestaurantHandler.Models.Interfaces;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Models;

public class UserDetails
{
    public required Guid Id { get; set; }

    public required string Email { get; set; }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    public string? PhoneNumber { get; set; }

    public string? ImgUrl { get; set; }

    public StatusAccountDetails? Status { get; set; }
    public bool? EmailConfirmed { get; set; }

    [Required]
    public required ICollection<RoleDetails>? Roles { get; set; }

    /// <summary>Faux tant que l'utilisateur n'a pas accepté la version courante des CGU.</summary>
    public bool TermsUpToDate { get; set; }

    public UserDetails() { }

    [SetsRequiredMembers]
    public UserDetails(UserApp user, List<RoleDetails>? roles)
    {
        Id = user.Id;
        Email = user.Email ?? user.UserName ?? "";
        FirstName = user.FirstName;
        LastName = user.LastName;
        PhoneNumber = user.PhoneNumber;
        ImgUrl = user.ImgUrl;

        Roles = roles;
        Status = user.Status is not null ? new StatusAccountDetails(user.Status) : null;
        EmailConfirmed = user.EmailConfirmed;
        TermsUpToDate = user.TermsVersion == HardCode.TERMS_VERSION;
    }
}

/// <summary>
/// Utilisateur vu par un Manager : <see cref="UserDetails"/> plus la date d'inscription.
/// Réservé aux endpoints [Authorize(Roles = "Manager")].
/// </summary>
public class UserAdminDetails : UserDetails
{
    /// <summary>Date d'inscription du compte.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    public UserAdminDetails() { }

    [SetsRequiredMembers]
    public UserAdminDetails(UserApp user, List<RoleDetails>? roles)
        : base(user, roles)
    {
        CreatedAt = user.CreatedAt;
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

    /// <summary>Pris en compte seulement à la création par un Manager ; l'inscription
    /// publique crée toujours un Client.</summary>
    public Guid RoleId { get; set; } = HardCode.ROLE_CLIENT;
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
    /// Vrai tant que le compte n'a pas de numéro de téléphone (cas d'un compte créé par Google) :
    /// le front le demande, au plus tard à la première commande (US-E01-3).
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

    /// <summary>Facultatif ; <c>null</c> ou vide efface le numéro.</summary>
    [Phone]
    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    public void UpdateUser(UserApp user)
    {
        user.FirstName = FirstName;
        user.LastName = LastName;
        user.PhoneNumber = string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim();
    }
}

public class UserInfosWithtoken
{
    [Required]
    public required string Token { get; set; }

    [Required]
    public required UserDetails User { get; set; }
}
