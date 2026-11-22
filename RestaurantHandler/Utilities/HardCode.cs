namespace RestaurantHandler.Utilities;

public static class HardCode
{
    // How long a buyer has to pay before the checkout expires and the held slots are
    // released. Drives the Stripe session expiry, the buyer-facing countdown, and the
    // recurring cleanup sweep, so all three always agree. Stripe enforces a 30-minute
    // minimum / 24-hour maximum on a checkout session's expiry.
    public static readonly TimeSpan CHECKOUT_WINDOW = TimeSpan.FromMinutes(30);

    // Langue par défaut / fallback pour les traductions des listes gérées par l'admin
    // (cf. CLAUDE.md Feature 14, TranslatableModelOption).
    public const string DEFAULT_LANGUAGE_CODE = "fr";

    // Version des CGU en vigueur. La changer redemande l'acceptation à tous les utilisateurs ;
    // doit rester alignée avec la date affichée sur la page /cgu côté Angular.
    public const string TERMS_VERSION = "2026-10-09";

    // Langues dans lesquelles l'admin peut saisir ces traductions — doit rester aligné
    // avec SUPPORTED_LANGUAGES côté Angular (language.service.ts).
    public static readonly string[] SUPPORTED_LANGUAGE_CODES = [DEFAULT_LANGUAGE_CODE, "en"];

    // Fenêtre après la fin du créneau pendant laquelle l'élève peut ouvrir un litige,
    // ET délai avant lequel le prof n'est pas encore payé. Une seule constante pour les
    // deux : tant que la fenêtre de litige ≤ délai de payout, un litige ne peut jamais
    // arriver après un versement → aucun clawback / Transfer Reversal à gérer.
    public static readonly TimeSpan DISPUTE_WINDOW = TimeSpan.FromDays(2) + TimeSpan.FromHours(2);

    // Share of every reservation the platform keeps. The teacher is credited the rest
    // (price * (1 - rate)) when the order is paid. Single source of truth for the split.
    public const decimal PLATFORM_COMMISSION_RATE = 0.05m;

    // Délai laissé au prof pour accepter/refuser une réservation sans paiement (prof pas
    // encore IsStripeConfirmed et/ou IsVerified). Passé ce délai, la réservation encore
    // PENDING est auto-rejetée et le créneau libéré (JobChron.ExpireStaleNoPaymentReservations).
    public static readonly TimeSpan TEACHER_RESPONSE_WINDOW = TimeSpan.FromHours(48);

    /// <summary>Essais erronés de code de présence avant verrouillage temporaire.</summary>
    public const int VALIDATION_CODE_MAX_ATTEMPTS = 5;

    /// <summary>Durée du verrou de saisie du code de présence.</summary>
    public static readonly TimeSpan VALIDATION_CODE_LOCKOUT = TimeSpan.FromMinutes(15);

    // Délai avant le cours en deçà duquel l'annulation de l'élève devient « tardive » :
    // il n'est plus remboursé qu'à hauteur de price * (1 - commission), la plateforme
    // gardant sa commission. Au-delà, remboursement intégral (c'est la plateforme qui
    // absorbe les frais Stripe, non restitués par Stripe sur un remboursement).
    public static readonly TimeSpan LATE_CANCELLATION_WINDOW = TimeSpan.FromHours(48);

    // Pénalité prélevée sur le prof qui annule un cours déjà payé : l'élève est remboursé
    // intégralement et le prof porte le coût (frais Stripe compris). Constante distincte de
    // PLATFORM_COMMISSION_RATE malgré la valeur identique — ce sont deux règles métier
    // indépendantes, l'une peut bouger sans l'autre.
    public const decimal TEACHER_CANCELLATION_FEE_RATE = 0.05m;

    // VAT rate applied to the lesson on the invoice. Stored prices are TTC (the amount
    // the student pays); the HT base and VAT amount are derived from it on the invoice.
    // Flat rate for now — to be refined later (per-teacher regime / franchise en base).
    public const decimal VAT_RATE = 0.20m;

    // Landing page "featured tutors" widget: how many teachers it shows, and how large a
    // top-rated pool to draw them from. The pool is bigger than the count so the widget
    // rotates among genuinely top-rated teachers instead of pinning the same 10 forever.
    public const int FEATURED_TEACHERS_COUNT = 10;
    public const int FEATURED_TEACHERS_POOL_SIZE = 30;

    // Suivi de cours (E20, ADR-0014). Le titre d'une liste suit la même borne que celui du
    // tableau ; la longueur de description se mesure sur le texte, sans les balises.
    public const int BOARD_TITLE_MAX_LENGTH = 64;
    public const int LIST_MAX_PER_BOARD = 20;
    public const int CARD_TITLE_MAX_LENGTH = 128;
    public const int CARD_DESCRIPTION_MAX_LENGTH = 10_000;

    // Envois groupés par e-mail (ADR-0023) : texte hors balises, et quota glissant par expéditeur.
    public const int MAIL_BROADCAST_SUBJECT_MAX_LENGTH = 200;
    public const int MAIL_BROADCAST_BODY_MAX_LENGTH = 20_000;
    public const int MAIL_BROADCAST_MAX_PER_HOUR = 10;
    // Description d'une formation : texte enrichi, mesuré comme celle d'une carte.
    public const int FORMATION_DESCRIPTION_MAX_LENGTH = 1_000;
    public const int FORMATION_DESCRIPTION_MAX_HTML_LENGTH = 10_000;
    // Description d'un cours (Cursus) : même règle, borne de l'ancien texte brut conservée.
    public const int CURSUS_DESCRIPTION_MAX_LENGTH = 512;
    public const int CURSUS_DESCRIPTION_MAX_HTML_LENGTH = 10_000;
    // Articles scientifiques : le texte d'une section se mesure sans les balises.
    public const int ARTICLE_TITLE_MAX_LENGTH = 200;
    public const int ARTICLE_SUBTITLE_MAX_LENGTH = 300;
    public const int ARTICLE_INTRO_MAX_LENGTH = 1000;
    public const int ARTICLE_SECTION_TITLE_MAX_LENGTH = 200;
    public const int ARTICLE_SECTION_TEXT_MAX_LENGTH = 20_000;
    public const int ARTICLE_SECTION_MAX_HTML_LENGTH = 100_000;
    public const int ARTICLE_MAX_SECTIONS = 50;
    public const int ARTICLE_MAX_REFERENCES = 100;
    public const int ARTICLE_MAX_TAGS = 10;
    public const int ARTICLE_RELATED_MAX_COUNT = 10;
    public const int ARTICLE_TAG_MAX_LENGTH = 40;
    public const int ARTICLE_EXCERPT_LENGTH = 300;
    public const int ARTICLE_COMMENT_MAX_LENGTH = 2_000;
    public static readonly string[] ARTICLE_LANGUAGES = ["fr", "en"];
    // Centre d'aide : la réponse se mesure sans les balises, comme une section d'article.
    public const int HELP_QUESTION_MAX_LENGTH = 200;
    public const int HELP_SUMMARY_MAX_LENGTH = 300;
    public const int HELP_ANSWER_TEXT_MAX_LENGTH = 20_000;
    public const int HELP_ANSWER_MAX_HTML_LENGTH = 100_000;
    public const int HELP_MAX_KEYWORDS = 15;
    public const int HELP_KEYWORD_MAX_LENGTH = 40;
    public const int HELP_SEARCH_MAX_RESULTS = 20;
    public const int HELP_SEARCH_MAX_TERMS = 8;
    public const int HELP_RELATED_COUNT = 4;
    public static readonly string[] BOARD_DEFAULT_LISTS = ["À faire", "En cours", "Fait"];

    // Pièces jointes des cartes (ADR-0015) et du chat (ADR-0016) : plus strict que
    // ALLOWED_EXTENSIONS et MAX_UPLOAD_SIZE_MB, vérifié avant l'envoi au stockage.
    public static readonly string[] ATTACHMENT_EXTENSIONS = [".jpg", ".jpeg", ".png", ".webp", ".pdf"];
    public const long ATTACHMENT_MAX_SIZE_BYTES = 10L * 1024 * 1024;
    public const int CARD_ATTACHMENT_MAX_PER_CARD = 10;
    public const int CHAT_ATTACHMENT_MAX_PER_RESERVATION = 20;

    // Roles
    public static Guid ROLE_SUPER_ADMIN => Guid.Parse("bde5556b-562d-431f-9ff9-d31a5f5cb8c5");
    public static Guid ROLE_ADMIN => Guid.Parse("4a5eaf2f-0496-4035-a4b7-9210da39501c");
    public static Guid ROLE_TEACHER => Guid.Parse("87a0a5ed-c7bb-4394-a163-7ed7560b3703");
    public static Guid ROLE_STUDENT => Guid.Parse("87a0a5ed-c7bb-4394-a163-7ed7560b4a01");
    public static Guid ROLE_EDITOR => Guid.Parse("c3f1e2a4-5b6d-4e7f-8a9b-0c1d2e3f4a5b");

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

    // Reservation Status
    public static Guid RESERVATION_PENDING => Guid.Parse("2EC60A91-AAB8-4753-A5D8-B131B9441E77");
    public static Guid RESERVATION_ACCEPTED => Guid.Parse("32D854B6-6D4E-445A-9209-31A492970F2D");
    public static Guid RESERVATION_REJECTED => Guid.Parse("CAAB85F5-D37B-4EA0-B035-5BA3CA8DD49F");
    public static Guid RESERVATION_DONE => Guid.Parse("6D281AEC-D093-4071-8BF4-C8363361B5B4");
    public static Guid RESERVATION_CANCELLED => Guid.Parse("C5420623-8B51-4CE9-9EC1-3BC546B9FFFF");
    public static Guid RESERVATION_DELIVERED => Guid.Parse("B9A5C1E2-3F47-4D8A-9C1B-7E2F4A6D8C30");
    public static Guid RESERVATION_PAYABLE => Guid.Parse("C4D6E8F0-1A2B-4C3D-8E5F-6A7B8C9D0E11");
    public static Guid RESERVATION_DISPUTED => Guid.Parse("D2E4F6A8-9B1C-4D3E-AF5B-1C2D3E4F5A62");

    // Levels
    public static Guid LEVEL_BEGINNER => Guid.Parse("eb4bd576-6855-4123-bb92-e921c8610542");
    public static Guid LEVEL_INTERMEDIATE => Guid.Parse("19702cef-0a3b-46f8-932b-cf78634e741d");
    public static Guid LEVEL_ADVANCED => Guid.Parse("a518f61e-8a19-44e6-b365-fb285ad0811e");
    public static Guid LEVEL_ALL => Guid.Parse("b70134eb-060c-4069-a325-1251c75a1ac9");

    // Categories
    public static Guid CATEGORY_SOFT => Guid.Parse("86969bd8-e51f-4558-ac44-fc159ed31c53");
    public static Guid CATEGORY_TECHNICS => Guid.Parse("a7f8b05d-3d2d-43fa-870d-987c21f4e41d");
    public static Guid CATEGORY_FRONT => Guid.Parse("0b6865c4-76fc-4caa-8171-07f449ca6e5c");
    public static Guid CATEGORY_BACK => Guid.Parse("4133e2f8-f0e0-44f8-8cfb-8a8e1aaa86d7");

    // Status Transaction
    public static Guid STATUS_TRANSACTION_PAID =>
        Guid.Parse("73f7fa42-196b-4040-b727-64a7b1e56458");
    public static Guid STATUS_TRANSACTION_FAILED =>
        Guid.Parse("584f233a-bf58-4db9-a24e-90baac3f6d42");
    public static Guid STATUS_TRANSACTION_PENDING =>
        Guid.Parse("ead0ecc1-a58d-4436-a87f-89c2dbc665a8");

    // Type Teacher Transactions
    public static Guid TYPE_TEACHER_TRANSACTION_PAYMENT =>
        Guid.Parse("7305a3d5-bfa9-41ce-be10-174c406cb842");
    public static Guid TYPE_TEACHER_TRANSACTION_PAYOUT =>
        Guid.Parse("27aec3d1-dd41-4728-a29e-473da46779d9");
    public static Guid TYPE_TEACHER_TRANSACTION_REFUND =>
        Guid.Parse("50412518-6c82-40c1-b6bf-b9c7aadf67d1");

    /// <summary>Frais d'annulation prélevé sur le prof qui annule un cours payé.</summary>
    public static Guid TYPE_TEACHER_TRANSACTION_PENALTY =>
        Guid.Parse("1f6c2a4e-8d35-4b91-9c72-0e5a3b7d1c48");

    /// <summary>Récupération d'un crédit DÉJÀ versé au prof (remboursement post-payout).</summary>
    public static Guid TYPE_TEACHER_TRANSACTION_CLAWBACK =>
        Guid.Parse("3b9e5d17-42a6-4f80-ae13-6c8f2d90b5a7");

    // Les seuls types qui constituent une DETTE du prof envers la plateforme, recouvrée par
    // compensation sur ses versements suivants (cf. JobChron.PayoutTeacherBalances).
    //
    // INVARIANT — ne PAS confondre avec une simple ligne négative : une ligne REFUND est
    // toujours adossée à la ligne PAYMENT de la même réservation et les deux s'annulent
    // (crédit gelé neutralisé, aucun argent n'est parti) → ce n'est pas une dette, et la
    // recouvrer double-compterait le remboursement. PENALTY / CLAWBACK n'ont, eux, aucun
    // crédit vivant en regard : c'est de l'argent que le prof doit réellement.
    public static readonly Guid[] DEBT_TRANSACTION_TYPES =
    [
        TYPE_TEACHER_TRANSACTION_PENALTY,
        TYPE_TEACHER_TRANSACTION_CLAWBACK,
    ];

    // Type addresses
    public static Guid TYPE_ADDRESS_MAIN => Guid.Parse("e1fee3ea-6190-48c3-8e40-c1f053fea79d");
    public static Guid TYPE_ADDRESS_BILLING => Guid.Parse("b8b8a8fc-ca60-440b-815f-1e44b89c9803");

    // Type slots
    public static Guid TYPE_SLOT_PRESENTIAL => Guid.Parse("79f538c3-5f2b-4e45-a5f8-4d7cda8b3df8");
    public static Guid TYPE_SLOT_VISIO => Guid.Parse("4043e32b-4d92-49b5-b885-505155ff2fe9");
    public static Guid TYPE_SLOT_ALL => Guid.Parse("c25c18a2-af88-4132-a27a-0025417edb56");

    // langues
    public static Guid LANGUAGE_FRENCH => Guid.Parse("52b54b82-1f37-4a66-a263-708b53cd685d");
    public static Guid LANGUAGE_ENGLISH => Guid.Parse("3aa916ed-53d2-4f93-80e9-b49171a7ebe1");
    public static Guid LANGUAGE_ARAB => Guid.Parse("ff34f5ba-6201-45bf-9217-dcda019976a3");

    // Status Orders
    public static Guid STATUS_ORDER_PENDING => Guid.Parse("4470502b-fce9-4d5f-913c-3340af86b532");
    public static Guid STATUS_ORDER_PAID => Guid.Parse("36fe7f5e-61f3-4ce1-88bb-8942d3b094a1");
    public static Guid STATUS_ORDER_REFUNDED => Guid.Parse("8b84e43c-d208-4c18-9c23-2b13438eb109");
    public static Guid STATUS_ORDER_REFUNDED_PARTIALLY =>
        Guid.Parse("2a6822ed-9116-47b7-a225-be832db4beb8");

    // Dispute Status (Feature 8)
    public static Guid DISPUTE_STATUS_OPEN => Guid.Parse("1a2b3c4d-5e6f-4a1b-9c2d-3e4f5a6b7c8d");
    public static Guid DISPUTE_STATUS_RESOLVED => Guid.Parse("2b3c4d5e-6f7a-4b2c-8d3e-4f5a6b7c8d9e");

    // Dispute Outcome (Feature 8)
    public static Guid DISPUTE_OUTCOME_TEACHER => Guid.Parse("3c4d5e6f-7a8b-4c3d-9e4f-5a6b7c8d9e0f");
    public static Guid DISPUTE_OUTCOME_STUDENT => Guid.Parse("4d5e6f7a-8b9c-4d4e-af5a-6b7c8d9e0f1a");
    public static Guid DISPUTE_OUTCOME_PARTIAL => Guid.Parse("5e6f7a8b-9c0d-4e5f-ba6b-7c8d9e0f1a2b");

    // docummment types
    public static Guid DOCUMENT_ID => Guid.Parse("3817d1eb-594e-40ba-8544-959121aaa179");
    public static Guid DOCUMENT_DIPLOME => Guid.Parse("d918182e-d9d7-452c-9607-ab83f78139d0");
    public static Guid DOCUMENT_SIRET => Guid.Parse("8bcfa1b8-70bb-4e9c-a1d1-e1c64230a579");
    public static Guid DOCUMENT_OTHER => Guid.Parse("f709ecfd-82f3-4d07-851f-419e9016135f");

    /// <summary>Justificatif de domicile — second type requis par défaut (Feature 10).</summary>
    public static Guid DOCUMENT_PROOF_OF_ADDRESS =>
        Guid.Parse("6a1d9e3b-7c4f-4a2e-9b8d-2f5c1e6a8d3f");
    public static Guid PAYMENT_METHOD_CARD => Guid.Parse("f6913a94-81cc-4f8a-ae42-b2c89bc5fb85");
}

public enum StatusReservationCode
{
    Pending,
    Accepted,
    Rejected,
    Done,
    Cancelled,

    // Ajouts state machine (Features 7-8). Append-only : ne pas réordonner
    // (le Code est persisté en int, réordonner casserait les données existantes).

    /// <summary>Cours confirmé tôt via code de présence (chemin heureux, optionnel).</summary>
    Delivered,

    /// <summary>Créneau terminé, fenêtre de litige écoulée, aucun litige : versable au prof.</summary>
    Payable,

    /// <summary>Litige ouvert par l'élève : gèle le crédit wallet, exclu du payout.</summary>
    Disputed,
}

public enum RoleEnum
{
    Student,
    Teacher,
    Admin,
    SuperAdmin,

    /// <summary>Prof autorisé par un admin à rédiger des articles publics (cumulé avec Teacher).</summary>
    Editor,
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

public enum AddressTypeEnum
{
    Main,
    Billing,
}

public enum TransactionStatusEnum
{
    Pending,
    Paid,
    Failed,
}

public enum TeacherTransactionTypeEnum
{
    Payment,
    Refund,
    Payout,

    // Append-only : ne pas réordonner (le nom est seedé, l'ordre porte le sens historique).

    /// <summary>Frais d'annulation prof — dette, recouvrée sur les versements suivants.</summary>
    Penalty,

    /// <summary>Crédit déjà versé au prof et à récupérer — dette, même mécanique.</summary>
    Clawback,
}
public enum OrderStatusEnum
{
    Pending,
    Paid,
    FullRefund,
    PartialRefund,
}

/// <summary>Statut de vérification SIRET auprès de l'INSEE Sirene à l'inscription.
/// Persisté en int — append-only : ne pas réordonner.</summary>
public enum SiretVerificationStatusEnum
{
    Verified,        // établissement + unité légale actifs chez l'INSEE
    FlaggedForReview, // SIRET trouvé mais fermé (établissement et/ou unité légale) — admin doit vérifier
}

public enum CursusLevelEnum
{
    All,
    Beginner,
    Intermediate,
    Advanced,
}

public enum LanguageEnum
{
    French,
    English,
    Arab,
}

public enum CursusCategoryEnum
{
    Soft,
    Technics,
    Front,
    Back,
}

public enum SlotTypeEnum
{
    Presential,
    Visio,
    All,
}

public enum EnumMethodPayment
{
    Card
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
    Chat,
    Writing
}

public enum NotificationTypeEnum
{
    Account,
    Reservation,
    Payment,
    Chat,
    Cursus,

    // Append-only : ne pas réordonner.
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

public enum MessageTypeEnum
{
    Notification,
    Email,
    Chat,
    Ping
}

public enum StripeWebhookEventEnum
{
    Unknown,
    CheckoutSessionCompleted,
    CheckoutSessionExpired,
    PaymentIntentSucceeded,
    PaymentIntentPaymentFailed,
    AccountUpdated,
    ChargeRefunded,
}

public static class StripeWebhookEventExtensions
{
    // Stripe event type strings contain dots, so they can't be enum names directly.
    // This keeps the raw strings in one place instead of scattered switch literals.
    public static StripeWebhookEventEnum ToStripeWebhookEvent(this string eventType) =>
        eventType switch
        {
            "checkout.session.completed" => StripeWebhookEventEnum.CheckoutSessionCompleted,
            "checkout.session.expired" => StripeWebhookEventEnum.CheckoutSessionExpired,
            "payment_intent.succeeded" => StripeWebhookEventEnum.PaymentIntentSucceeded,
            "payment_intent.payment_failed" => StripeWebhookEventEnum.PaymentIntentPaymentFailed,
            "account.updated" => StripeWebhookEventEnum.AccountUpdated,
            "charge.refunded" => StripeWebhookEventEnum.ChargeRefunded,
            _ => StripeWebhookEventEnum.Unknown,
        };
}

/// <summary>Importance d'une carte de suivi. Valeur numérique persistée : ajout seul, ne jamais
/// réordonner.</summary>
public enum BoardCardImportance
{
    Basse = 0,
    Normale = 1,
    Haute = 2,
    Urgente = 3,
}
