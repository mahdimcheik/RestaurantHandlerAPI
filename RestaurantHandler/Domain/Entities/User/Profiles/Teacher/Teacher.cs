using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using RestaurantHandler.Models.Interfaces;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Models;

public class Teacher : BaseModel
{
    [Required]
    [ForeignKey(nameof(User))]
    public Guid UserId { get; set; }
    public required UserApp? User { get; set; }
    public string? LinkedIn { get; set; }
    public string? FaceBook { get; set; }
    public string? GitHub { get; set; }
    public string? Twitter { get; set; }
    public string? Instagram { get; set; }
    public string? Website { get; set; }

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    /// <summary>Choix du prof : numéro visible des utilisateurs connectés sur son profil
    /// public. Ne jamais sérialiser <see cref="PhoneNumber"/> dans une réponse anonyme.</summary>
    public bool ShowPhoneNumber { get; set; } = false;

    /// <summary>Choix du prof : email révélable aux utilisateurs connectés depuis son profil
    /// public. Masqué par défaut ; l'email ne sort jamais dans une réponse anonyme.</summary>
    public bool ShowEmail { get; set; } = false;
    [Required]
    public required decimal PriceIndicative { get; set; }
    public long? Siret { get; set; }

    /// <summary>Vrai une fois un SIRET vérifié auprès de l'INSEE (à l'inscription ou via
    /// « passer en mode professionnel »). Toujours gater la logique métier/l'affichage
    /// sur ce flag, jamais sur <see cref="SiretVerificationStatus"/> seul : ce dernier
    /// garde sa valeur par défaut pour un prof non-pro qui n'a jamais été vérifié.</summary>
    public bool IsProfessional { get; set; } = false;

    // SIRET verification status
    [Required]
    public required SiretVerificationStatusEnum SiretVerificationStatus { get; set; } = SiretVerificationStatusEnum.Verified;
    public DateTimeOffset? SiretVerifiedAt { get; set; }
    public string? SiretCompanyName { get; set; }
    public string? SiretCompanyAddress { get; set; }
    public string? SiretActivityCode { get; set; }
    public string? SiretActivityNomenclature { get; set; }

    /// <summary>Vérification documentaire (Feature 10) : vrai quand tous les documents requis
    /// (types <see cref="Files.PrivacyDocumentType.IsRequiredByDefault"/> + demandes admin
    /// ad hoc <see cref="Files.TeacherDocumentRequest"/>) ont un document validé. Recalculé par
    /// <see cref="Utilities.TeacherVerificationHelper"/>, jamais posé à la main.</summary>
    public bool IsVerified { get; set; } = false;
    public DateTimeOffset? VerifiedAt { get; set; }

    public ICollection<Experience> Experiences { get; set; } = new List<Experience>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

    [SetsRequiredMembers]
    public Teacher()
    {
    }

    // For admin user creation (no SIRET verification)
    [SetsRequiredMembers]
    public Teacher(TeacherCreate teacherCreate, Guid userId)
    {
        Id = userId;
        UserId = userId;
        Siret = teacherCreate.Siret;
        ShowEmail = teacherCreate.ShowEmail;
        IsProfessional = teacherCreate.Siret is not null;
    }

    // For public registration; siretVerification is null when the teacher registers
    // without a SIRET (professional mode is then chosen later via SwitchToProfessional).
    [SetsRequiredMembers]
    public Teacher(TeacherCreate teacherCreate, Guid userId, SiretVerificationResult? siretVerification)
    {
        Id = userId;
        UserId = userId;
        Siret = teacherCreate.Siret;
        ShowEmail = teacherCreate.ShowEmail;

        if (siretVerification is not null)
        {
            ApplySiretVerification(siretVerification);
        }
    }

    /// <summary>Pose les champs de vérification SIRET (dénomination, adresse, activité INSEE)
    /// et marque le prof comme professionnel. Partagé entre l'inscription et
    /// <see cref="Services.TeacherProfileService.SwitchToProfessionalAsync"/> pour ne pas dupliquer le mapping.</summary>
    public void ApplySiretVerification(SiretVerificationResult siretVerification)
    {
        IsProfessional = true;
        SiretVerificationStatus = siretVerification.Status == SiretVerificationApiStatus.Active
            ? SiretVerificationStatusEnum.Verified
            : SiretVerificationStatusEnum.FlaggedForReview;
        SiretVerifiedAt = DateTimeOffset.UtcNow;
        SiretCompanyName = siretVerification.CompanyName;
        SiretCompanyAddress = siretVerification.CompanyAddress;
        SiretActivityCode = siretVerification.ActivityCode;
        SiretActivityNomenclature = siretVerification.ActivityNomenclature;
    }
}
