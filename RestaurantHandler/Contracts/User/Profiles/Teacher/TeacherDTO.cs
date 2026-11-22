using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using RestaurantHandler.Models;
using RestaurantHandler.Models.Files;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Models;

public class TeacherDetails
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
    public List<CursusDetails> Cursuses { get; set; }
    public string? LinkedIn { get; set; }
    public string? FaceBook { get; set; }
    public string? GitHub { get; set; }
    public string? Twitter { get; set; }
    public string? Instagram { get; set; }
    public string? Website { get; set; }

    /// <summary>Vrai si le prof a choisi d'afficher son numéro (récupérable via
    /// <c>GET /teachers/user/{id}/phone</c> par un utilisateur connecté).</summary>
    public bool ShowPhoneNumber { get; set; }

    /// <summary>Renseigné uniquement pour le prof lui-même (my-profile, update-profile) :
    /// ce DTO est aussi servi par des endpoints [AllowAnonymous].</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>Vrai si le prof a choisi d'afficher son email (récupérable via
    /// <c>GET /teachers/user/{id}/email</c> par un utilisateur connecté).</summary>
    public bool ShowEmail { get; set; }
    public decimal PriceIndicative { get; set; }
    //public long? Siret { get; set; }
    public bool IsProfessional { get; set; } = false;
    public bool IsStripeConfirmed { get; set; } = false;
    //public string? StripeAccount { get; set; }
    public double AverageRating { get; set; }
    public int ReviewsCount { get; set; }

    /// <summary>Résultat de la vérification du SIRET auprès de l'INSEE à l'inscription.</summary>
    public SiretVerificationStatusEnum SiretVerificationStatus { get; set; }
    public DateTimeOffset? SiretVerifiedAt { get; set; }

    /// <summary>Vérification documentaire (Feature 10) : cf. <see cref="Teacher.IsVerified"/>.</summary>
    public bool IsVerified { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }

    // La dénomination et l'adresse renvoyées par l'INSEE ne sont volontairement PAS ici :
    // ce DTO est servi par des endpoints [AllowAnonymous] (cf. SlotsController), et l'adresse
    // de l'établissement est souvent le domicile du prof. Elles vivent dans TeacherAdminDetails.

    public UserMinimalDetails? User { get; set; }

    public TeacherDetails() { }

    /// <param name="includeEmail">Vrai seulement pour un appelant authentifié lié au prof
    /// (réservation, commande, portefeuille) : ce DTO est aussi servi en anonyme.</param>
    [SetsRequiredMembers]
    public TeacherDetails(Teacher teacher, bool includeEmail = false)
    {
        Id = teacher.Id;

        CreatedAt = teacher.CreatedAt;
        UpdatedAt = teacher.UpdatedAt;

        LinkedIn = teacher.LinkedIn;
        FaceBook = teacher.FaceBook;
        GitHub = teacher.GitHub;
        Twitter = teacher.Twitter;
        Instagram = teacher.Instagram;
        Website = teacher.Website;
        ShowPhoneNumber = teacher.ShowPhoneNumber && !string.IsNullOrWhiteSpace(teacher.PhoneNumber);
        ShowEmail = teacher.ShowEmail;
        PriceIndicative = teacher.PriceIndicative;
        //Siret = teacher.Siret;
        IsProfessional = teacher.IsProfessional;
        IsStripeConfirmed = teacher.IsStripeConfirmed;
        //StripeAccount = teacher.StripeAccount;
        AverageRating = teacher.AverageRating;
        ReviewsCount = teacher.ReviewsCount;
        SiretVerificationStatus = teacher.SiretVerificationStatus;
        SiretVerifiedAt = teacher.SiretVerifiedAt;
        IsVerified = teacher.IsVerified;
        VerifiedAt = teacher.VerifiedAt;
        Cursuses = teacher.Cursuses.Select(p => new CursusDetails(p)).ToList();
        User =
            teacher.User != null
                ? new UserMinimalDetails
                {
                    Id = teacher.User.Id,
                    FirstName = teacher.User.FirstName,
                    LastName = teacher.User.LastName,
                    Email = includeEmail ? teacher.User.Email ?? teacher.User.UserName ?? "" : "",
                    ImgUrl = teacher.User.ImgUrl
                }
                : null;
    }
}

/// <summary>
/// Vue enseignant réservée à l'administration (cf. Feature 10 — certification) : surensemble de
/// <see cref="TeacherDetails"/> enrichi des champs sensibles.
///
/// Le SIRET, la dénomination et l'adresse renvoyées par l'INSEE ainsi que le compte Stripe Connect
/// n'ont pas leur place dans <see cref="TeacherDetails"/> : ce dernier est sérialisé par des
/// endpoints [AllowAnonymous] (listing des créneaux), et l'adresse de l'établissement est
/// fréquemment le domicile du prof.
/// </summary>
public class TeacherAdminDetails : TeacherDetails
{
    public long? Siret { get; set; }

    /// <summary>Identifiant du compte Stripe Connect Express, <c>null</c> tant que non onboardé.</summary>
    public string? StripeAccount { get; set; }

    /// <summary>Dénomination INSEE, ou « prénom nom » pour un entrepreneur individuel.</summary>
    public string? SiretCompanyName { get; set; }
    public string? SiretCompanyAddress { get; set; }

    /// <summary>Code d'activité principale (APE/NAF) déclaré à l'INSEE, ex. « 85.59A ».</summary>
    public string? SiretActivityCode { get; set; }
    public string? SiretActivityNomenclature { get; set; }

    public TeacherAdminDetails() { }

    [SetsRequiredMembers]
    public TeacherAdminDetails(Teacher teacher)
        : base(teacher)
    {
        Siret = teacher.Siret;
        StripeAccount = teacher.StripeAccount;
        SiretCompanyName = teacher.SiretCompanyName;
        SiretCompanyAddress = teacher.SiretCompanyAddress;
        SiretActivityCode = teacher.SiretActivityCode;
        SiretActivityNomenclature = teacher.SiretActivityNomenclature;
    }
}

/// <summary>
/// Vue admin du dossier de vérification documentaire d'un prof (Feature 10) :
/// <c>GET /UserManagement/teachers/{userId}/documents</c>. Regroupe les documents vivants
/// (avec l'état de revue), les demandes ad hoc encore ouvertes, et l'état de vérification.
/// </summary>
public class TeacherCertificationOverview
{
    public bool IsVerified { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public List<PrivacyDocumentDetails> Documents { get; set; } = new();
    public List<TeacherDocumentRequestDetails> OpenRequests { get; set; } = new();
}

/// <summary>
/// DTO pour la création d'un profil enseignant
/// </summary>
public class TeacherCreate {
    [Siret]
    public long? Siret { get; set; }

    /// <summary>Choix fait à l'inscription : email révélable sur le profil public.</summary>
    public bool ShowEmail { get; set; }
}

/// <summary>
/// DTO pour passer un prof en mode professionnel après inscription : déclenche la
/// vérification INSEE du SIRET (cf. <see cref="Services.TeacherProfileService.SwitchToProfessionalAsync"/>).
/// </summary>
public class TeacherSwitchToProfessional
{
    [Required]
    [Siret]
    public long Siret { get; set; }
}

public class TeacherUpdate
{
    public string? LinkedIn { get; set; }
    public string? FaceBook { get; set; }
    public string? GitHub { get; set; }
    public string? Twitter { get; set; }
    public string? Instagram { get; set; }
    public string? Website { get; set; }

    [MaxLength(20)]
    [RegularExpression(PhonePattern, ErrorMessage = "Numéro de téléphone invalide")]
    public string? PhoneNumber { get; set; }

    public bool ShowPhoneNumber { get; set; }
    public bool ShowEmail { get; set; }
    public decimal PriceIndicative { get; set; }

    public const string PhonePattern = @"^\+?[0-9 .\-()]{6,20}$";

    public void UpdateTeacher(Teacher teacher)
    {
        // teacher.Title = Title;
        // teacher.Description = Description;
        teacher.LinkedIn = LinkedIn;
        teacher.FaceBook = FaceBook;
        teacher.GitHub = GitHub;
        teacher.Twitter = Twitter;
        teacher.Instagram = Instagram;
        teacher.Website = Website;
        teacher.PhoneNumber = string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim();
        teacher.ShowPhoneNumber = ShowPhoneNumber && teacher.PhoneNumber is not null;
        teacher.ShowEmail = ShowEmail;
        teacher.PriceIndicative = PriceIndicative;
    }
}
