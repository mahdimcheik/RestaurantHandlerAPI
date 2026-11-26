namespace RestaurantHandler.Utilities;

public static class HardCode
{
    // Langue par défaut / fallback pour les traductions des listes gérées par l'admin
    // (cf. TranslatableModelOption).
    public const string DEFAULT_LANGUAGE_CODE = "fr";

    // Version des CGU en vigueur. La changer redemande l'acceptation à tous les utilisateurs ;
    // doit rester alignée avec la date affichée sur la page /cgu côté Angular.
    public const string TERMS_VERSION = "2026-10-09";

    // Statut de l'enveloppe Response renvoyé par POST /auth/google quand le compte Google est
    // inconnu et que les consentements manquent : le front propose alors de finaliser le compte.
    public const int GOOGLE_CONSENT_REQUIRED_STATUS = 428;

    // Langues dans lesquelles l'admin peut saisir ces traductions — doit rester aligné
    // avec SUPPORTED_LANGUAGES côté Angular (language.service.ts).
    public static readonly string[] SUPPORTED_LANGUAGE_CODES = [DEFAULT_LANGUAGE_CODE, "en"];

    // Nom affiché dans les e-mails et les notifications, en attendant le nom commercial du
    // restaurant (docs/50-feuille-de-route/02-questions-ouvertes.md, question 14).
    public const string APP_NAME = "RestaurantHandler";
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
    // Manager : tout le personnel du restaurant (carte, commandes, utilisateurs, Hangfire).
    // Client : rôle par défaut, le seul attribuable par l'inscription publique.
    public static Guid ROLE_MANAGER => Guid.Parse("87a0a5ed-c7bb-4394-a163-7ed7560b3703");
    public static Guid ROLE_CLIENT => Guid.Parse("87a0a5ed-c7bb-4394-a163-7ed7560b4a01");

    // AccountStatuses
    public static Guid ACCOUNT_ACTIVE => Guid.Parse("12e65875-bec4-422a-aad4-c28de0e06fff");
    public static Guid ACCOUNT_PENDING => Guid.Parse("acc9ddb6-824b-42cb-8276-5d91b6af2003");
    public static Guid ACCOUNT_BANNED => Guid.Parse("fd2621ef-85f0-46a6-acdd-c77fb111630f");

}

public enum RoleEnum
{
    Client,
    Manager,
}

public enum AccountStatusEnum
{
    Active,
    Pending,
    Banned,
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
    Order,
}
