namespace RestaurantHandler.Utilities;

public static class HardCode
{
    // Langue par défaut / fallback pour les traductions des listes gérées par l'admin
    // (cf. CLAUDE.md Feature 14, TranslatableModelOption).
    public const string DEFAULT_LANGUAGE_CODE = "fr";

    // Version des CGU en vigueur. La changer redemande l'acceptation à tous les utilisateurs ;
    // doit rester alignée avec la date affichée sur la page /cgu côté Angular.
    public const string TERMS_VERSION = "2026-10-09";

    // Langues dans lesquelles l'admin peut saisir ces traductions — doit rester aligné
    // avec SUPPORTED_LANGUAGES côté Angular (language.service.ts).
    public static readonly string[] SUPPORTED_LANGUAGE_CODES = [DEFAULT_LANGUAGE_CODE, "en"];

    // Widget « profs mis en avant » de la page d'accueil : nombre de profs affichés.
    public const int FEATURED_TEACHERS_COUNT = 10;

    // Envois groupés par e-mail (ADR-0023) : texte hors balises, et quota glissant par expéditeur.
    public const int MAIL_BROADCAST_SUBJECT_MAX_LENGTH = 200;
    public const int MAIL_BROADCAST_BODY_MAX_LENGTH = 20_000;
    public const int MAIL_BROADCAST_MAX_PER_HOUR = 10;
    // Description d'une formation : texte enrichi, mesuré comme celle d'une carte.
    public const int FORMATION_DESCRIPTION_MAX_LENGTH = 1_000;
    public const int FORMATION_DESCRIPTION_MAX_HTML_LENGTH = 10_000;
    // Centre d'aide : la réponse se mesure sans les balises.
    public static readonly string[] HELP_LANGUAGES = ["fr", "en"];
    public const int HELP_QUESTION_MAX_LENGTH = 200;
    public const int HELP_SUMMARY_MAX_LENGTH = 300;
    public const int HELP_ANSWER_TEXT_MAX_LENGTH = 20_000;
    public const int HELP_ANSWER_MAX_HTML_LENGTH = 100_000;
    public const int HELP_MAX_KEYWORDS = 15;
    public const int HELP_KEYWORD_MAX_LENGTH = 40;
    public const int HELP_SEARCH_MAX_RESULTS = 20;
    public const int HELP_SEARCH_MAX_TERMS = 8;
    public const int HELP_RELATED_COUNT = 4;

    // Pièces jointes : plus strict que ALLOWED_EXTENSIONS et MAX_UPLOAD_SIZE_MB, vérifié avant
    // l'envoi au stockage (cf. AttachmentRules).
    public static readonly string[] ATTACHMENT_EXTENSIONS = [".jpg", ".jpeg", ".png", ".webp", ".pdf"];
    public const long ATTACHMENT_MAX_SIZE_BYTES = 10L * 1024 * 1024;

    // Roles
    public static Guid ROLE_SUPER_ADMIN => Guid.Parse("bde5556b-562d-431f-9ff9-d31a5f5cb8c5");
    public static Guid ROLE_ADMIN => Guid.Parse("4a5eaf2f-0496-4035-a4b7-9210da39501c");
    public static Guid ROLE_TEACHER => Guid.Parse("87a0a5ed-c7bb-4394-a163-7ed7560b3703");
    public static Guid ROLE_STUDENT => Guid.Parse("87a0a5ed-c7bb-4394-a163-7ed7560b4a01");

    // Ligne unique des paramètres de la plateforme (mode maintenance)
    public static Guid PLATFORM_SETTINGS_ID => Guid.Parse("5f0c6a52-7d0e-4b8f-9a39-2a1f7c3e9d10");

    // Genders
    public static Guid GENDER_MALE => Guid.Parse("DCB8B01B-205A-4EA2-A281-004A7B1BB972");
    public static Guid GENDER_FEMALE => Guid.Parse("B68C151B-DB34-462D-A65C-90989CC96E5E");
    public static Guid GENDER_OTHER => Guid.Parse("B07B2445-F39C-4B26-8FFE-E40FE561D8BC");

    // AccountStatuses
    public static Guid ACCOUNT_ACTIVE => Guid.Parse("12e65875-bec4-422a-aad4-c28de0e06fff");
    public static Guid ACCOUNT_PENDING => Guid.Parse("acc9ddb6-824b-42cb-8276-5d91b6af2003");
    public static Guid ACCOUNT_BANNED => Guid.Parse("fd2621ef-85f0-46a6-acdd-c77fb111630f");

    // docummment types
    public static Guid DOCUMENT_ID => Guid.Parse("3817d1eb-594e-40ba-8544-959121aaa179");
    public static Guid DOCUMENT_DIPLOME => Guid.Parse("d918182e-d9d7-452c-9607-ab83f78139d0");
    public static Guid DOCUMENT_SIRET => Guid.Parse("8bcfa1b8-70bb-4e9c-a1d1-e1c64230a579");
    public static Guid DOCUMENT_OTHER => Guid.Parse("f709ecfd-82f3-4d07-851f-419e9016135f");

    /// <summary>Justificatif de domicile — second type requis par défaut (Feature 10).</summary>
    public static Guid DOCUMENT_PROOF_OF_ADDRESS =>
        Guid.Parse("6a1d9e3b-7c4f-4a2e-9b8d-2f5c1e6a8d3f");
}

public enum RoleEnum
{
    Student,
    Teacher,
    Admin,
    SuperAdmin,
}

public enum GenderEnum
{
    Male,
    Female,
    Other,
}

public enum AccountStatusEnum
{
    Active,
    Pending,
    Banned,
}

/// <summary>Statut de vérification SIRET auprès de l'INSEE Sirene à l'inscription.
/// Persisté en int — append-only : ne pas réordonner.</summary>
public enum SiretVerificationStatusEnum
{
    Verified,        // établissement + unité légale actifs chez l'INSEE
    FlaggedForReview, // SIRET trouvé mais fermé (établissement et/ou unité légale) — admin doit vérifier
}

public enum DocumentTypeEnum
{
    Identification,
    Diploma,
    CompanyRegistration,
    Other,

    // Append-only : ne pas réordonner (le nom est seedé, l'ordre porte le sens historique).
    ProofOfAddress,
}

public enum SignalRNotificationTypeEnum
{
    Message,
    Ping,
    Notification,
}

public enum NotificationTypeEnum
{
    Account,
    Document,
}

/// <summary>Cycle de vie de la revue d'un document de vérification (Feature 10).
/// Persisté en int — append-only : ne pas réordonner.</summary>
public enum DocumentReviewStatusEnum
{
    Pending, // envoyé par le prof, en attente de revue admin
    Validated,
    Refused,
}
