using RestaurantHandler.Models;
using RestaurantHandler.Models.Files;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Persistence.Seed;

/// <summary>
/// Référentiels livrés avec le schéma (<c>HasData</c>). Toute modification d'une valeur ici
/// produit une migration : les identifiants sont ceux de <see cref="HardCode"/>.
/// </summary>
public static class ReferenceDataSeed
{
    public static void Apply(ModelBuilder builder)
    {
        // Seed Roles
        List<RoleApp> roles = new()
        {
            new RoleApp
            {
                Id = HardCode.ROLE_SUPER_ADMIN,
                Name = nameof(RoleEnum.SuperAdmin),
                NormalizedName = nameof(RoleEnum.SuperAdmin).ToUpper(),
                Color = "#3d82f2",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ConcurrencyStamp = "SUPERADMIN-STAMP-2025",
            },
            new RoleApp
            {
                Id = HardCode.ROLE_ADMIN,
                Name = nameof(RoleEnum.Admin),
                Color = "#31bdd6",
                NormalizedName = nameof(RoleEnum.Admin).ToUpper(),
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ConcurrencyStamp = "ADMIN-STAMP-2025",
            },
            new RoleApp
            {
                Id = HardCode.ROLE_TEACHER,
                Name = nameof(RoleEnum.Teacher),
                Color = "#31d68f",
                NormalizedName = nameof(Teacher).ToUpper(),
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ConcurrencyStamp = "TEACHER-STAMP-2025",
            },
            new RoleApp
            {
                Id = HardCode.ROLE_STUDENT,
                Name = nameof(RoleEnum.Student),
                NormalizedName = nameof(RoleEnum.Student).ToUpper(),
                Color = "#f57ad0",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ConcurrencyStamp = "STUDENT-STAMP-2025",
            },
            new RoleApp
            {
                Id = HardCode.ROLE_EDITOR,
                Name = nameof(RoleEnum.Editor),
                NormalizedName = nameof(RoleEnum.Editor).ToUpper(),
                Color = "#f59e0b",
                CreatedAt = new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc),
                ConcurrencyStamp = "EDITOR-STAMP-2026",
            },
        };
        builder.Entity<RoleApp>().HasData(roles);
        // Seed Genders
        List<Gender> genders = new()
        {
            new Gender
            {
                Id = HardCode.GENDER_FEMALE,
                Name = nameof(GenderEnum.Female),
                Translations = new() { ["fr"] = "Femme", ["en"] = "Female" },
                Color = "#ff69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new Gender
            {
                Id = HardCode.GENDER_MALE,
                Name = nameof(GenderEnum.Male),
                Translations = new() { ["fr"] = "Homme", ["en"] = "Male" },
                Color = "#fa69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new Gender
            {
                Id = HardCode.GENDER_OTHER,
                Name = nameof(GenderEnum.Other),
                Translations = new() { ["fr"] = "Autre", ["en"] = "Other" },
                Color = "#ab69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
        };

        builder.Entity<Gender>().HasData(genders);

        // Seed Account status
        List<StatusAccount> accountStatuses = new()
        {
            new StatusAccount
            {
                Id = HardCode.ACCOUNT_ACTIVE,
                Name = nameof(AccountStatusEnum.Active),
                Translations = new() { ["fr"] = "Actif", ["en"] = "Active" },
                Color = "#ff69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new StatusAccount
            {
                Id = HardCode.ACCOUNT_PENDING,
                Name = nameof(AccountStatusEnum.Pending),
                Translations = new() { ["fr"] = "En attente", ["en"] = "Pending" },
                Color = "#fa69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new StatusAccount
            {
                Id = HardCode.ACCOUNT_BANNED,
                Name = nameof(AccountStatusEnum.Banned),
                Translations = new() { ["fr"] = "Banni", ["en"] = "Banned" },
                Color = "#ab69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
        };
        builder.Entity<StatusAccount>().HasData(accountStatuses);

        // Seed type address
        List<TypeAddress> typeAddresses = new()
        {
            new TypeAddress
            {
                Id = HardCode.TYPE_ADDRESS_MAIN,
                Name = nameof(AddressTypeEnum.Main),
                DisplayName = "Principale",
                Translations = new() { ["fr"] = "Principale", ["en"] = "Main" },
                Color = "#ff69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new TypeAddress
            {
                Id = HardCode.TYPE_ADDRESS_BILLING,
                Name = nameof(AddressTypeEnum.Billing),
                DisplayName = "Facturation",
                Translations = new() { ["fr"] = "Facturation", ["en"] = "Billing" },
                Color = "#fa69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
        };
        builder.Entity<TypeAddress>().HasData(typeAddresses);

        // Seed Transaction Status
        List<StatusTransaction> statusTransactions = new()
        {
            new StatusTransaction
            {
                Id = HardCode.STATUS_TRANSACTION_PENDING,
                Name = nameof(TransactionStatusEnum.Pending),
                Translations = new() { ["fr"] = "En attente", ["en"] = "Pending" },
                Color = "#ff69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new StatusTransaction
            {
                Id = HardCode.STATUS_TRANSACTION_PAID,
                Name = nameof(TransactionStatusEnum.Paid),
                Translations = new() { ["fr"] = "Payé", ["en"] = "Paid" },
                Color = "#fa69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new StatusTransaction
            {
                Id = HardCode.STATUS_TRANSACTION_FAILED,
                Name = nameof(TransactionStatusEnum.Failed),
                Translations = new() { ["fr"] = "Échoué", ["en"] = "Failed" },
                Color = "#ab69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
        };

        builder.Entity<StatusTransaction>().HasData(statusTransactions);

        // Seed teacher transaction types
        List<TypeTeacherTransaction> typeTeacherTransactions = new()
        {
            new TypeTeacherTransaction
            {
                Id = HardCode.TYPE_TEACHER_TRANSACTION_PAYMENT,
                Name = nameof(TeacherTransactionTypeEnum.Payment),
                Translations = new() { ["fr"] = "Paiement", ["en"] = "Payment" },
                Color = "#ff69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new TypeTeacherTransaction
            {
                Id = HardCode.TYPE_TEACHER_TRANSACTION_REFUND,
                Name = nameof(TeacherTransactionTypeEnum.Refund),
                Translations = new() { ["fr"] = "Remboursement", ["en"] = "Refund" },
                Color = "#ab69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new TypeTeacherTransaction
            {
                Id = HardCode.TYPE_TEACHER_TRANSACTION_PAYOUT,
                Name = nameof(TeacherTransactionTypeEnum.Payout),
                Translations = new() { ["fr"] = "Versement", ["en"] = "Payout" },
                Color = "#69b4ff",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new TypeTeacherTransaction
            {
                Id = HardCode.TYPE_TEACHER_TRANSACTION_PENALTY,
                Name = nameof(TeacherTransactionTypeEnum.Penalty),
                Translations = new() { ["fr"] = "Pénalité", ["en"] = "Penalty" },
                Color = "#b46969",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new TypeTeacherTransaction
            {
                Id = HardCode.TYPE_TEACHER_TRANSACTION_CLAWBACK,
                Name = nameof(TeacherTransactionTypeEnum.Clawback),
                Translations = new() { ["fr"] = "Récupération", ["en"] = "Clawback" },
                Color = "#b48769",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
        };

        builder.Entity<TypeTeacherTransaction>().HasData(typeTeacherTransactions);

        // seed reservations status
        List<StatusReservation> statusReservations = new()
        {
            new StatusReservation
            {
                Id = HardCode.RESERVATION_PENDING,
                Name = nameof(StatusReservationCode.Pending),
                Translations = new() { ["fr"] = "En attente", ["en"] = "Pending" },
                Color = "#ff69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Code = StatusReservationCode.Pending,
            },
            new StatusReservation
            {
                Id = HardCode.RESERVATION_ACCEPTED,
                Name = nameof(StatusReservationCode.Accepted),
                Translations = new() { ["fr"] = "Acceptée", ["en"] = "Accepted" },
                Color = "#fa69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Code = StatusReservationCode.Accepted,
            },
            new StatusReservation
            {
                Id = HardCode.RESERVATION_DONE,
                Name = nameof(StatusReservationCode.Done),
                Translations = new() { ["fr"] = "Terminée", ["en"] = "Done" },
                Color = "#ab69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Code = StatusReservationCode.Done,
            },
            new StatusReservation
            {
                Id = HardCode.RESERVATION_REJECTED,
                Name = nameof(StatusReservationCode.Rejected),
                Translations = new() { ["fr"] = "Rejetée", ["en"] = "Rejected" },
                Color = "#ab69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Code = StatusReservationCode.Rejected,
            },
            new StatusReservation
            {
                Id = HardCode.RESERVATION_CANCELLED,
                Name = nameof(StatusReservationCode.Cancelled),
                Translations = new() { ["fr"] = "Annulée", ["en"] = "Cancelled" },
                Color = "#ab69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Code = StatusReservationCode.Cancelled,
            },
            new StatusReservation
            {
                Id = HardCode.RESERVATION_DELIVERED,
                Name = nameof(StatusReservationCode.Delivered),
                Translations = new() { ["fr"] = "Livrée", ["en"] = "Delivered" },
                Color = "#34c759",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Code = StatusReservationCode.Delivered,
            },
            new StatusReservation
            {
                Id = HardCode.RESERVATION_PAYABLE,
                Name = nameof(StatusReservationCode.Payable),
                Translations = new() { ["fr"] = "Payable", ["en"] = "Payable" },
                Color = "#0a84ff",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Code = StatusReservationCode.Payable,
            },
            new StatusReservation
            {
                Id = HardCode.RESERVATION_DISPUTED,
                Name = nameof(StatusReservationCode.Disputed),
                Translations = new() { ["fr"] = "En litige", ["en"] = "Disputed" },
                Color = "#ff3b30",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Code = StatusReservationCode.Disputed,
            },
        };
        builder.Entity<StatusReservation>().HasData(statusReservations);

        // seed order status
        List<StatusOrder> statusOrders = new()
        {
            new StatusOrder
            {
                Id = HardCode.STATUS_ORDER_PENDING,
                Name = nameof(OrderStatusEnum.Pending),
                Translations = new() { ["fr"] = "En attente", ["en"] = "Pending" },
                Color = "#ff69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new StatusOrder
            {
                Id = HardCode.STATUS_ORDER_PAID,
                Name = nameof(OrderStatusEnum.Paid),
                Translations = new() { ["fr"] = "Payée", ["en"] = "Paid" },
                Color = "#fa69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new StatusOrder
            {
                Id = HardCode.STATUS_ORDER_REFUNDED,
                Name = nameof(OrderStatusEnum.FullRefund),
                Translations = new() { ["fr"] = "Remboursée", ["en"] = "Refunded" },
                Color = "#ab69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new StatusOrder
            {
                Id = HardCode.STATUS_ORDER_REFUNDED_PARTIALLY,
                Name = nameof(OrderStatusEnum.PartialRefund),
                Translations = new() { ["fr"] = "Remboursée partiellement", ["en"] = "Partially refunded" },
                Color = "#ab69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
        };
        builder.Entity<StatusOrder>().HasData(statusOrders);

        // seed languages
        List<Language> languages = new()
        {
            new Language
            {
                Id = HardCode.LANGUAGE_ARAB,
                Name = nameof(LanguageEnum.Arab),
                Translations = new() { ["fr"] = "Arabe", ["en"] = "Arabic" },
                Color = "#ff69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new Language
            {
                Id = HardCode.LANGUAGE_FRENCH,
                Name = nameof(LanguageEnum.French),
                Translations = new() { ["fr"] = "Français", ["en"] = "French" },
                Color = "#fa69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new Language
            {
                Id = HardCode.LANGUAGE_ENGLISH,
                Name = nameof(LanguageEnum.English),
                Translations = new() { ["fr"] = "Anglais", ["en"] = "English" },
                Color = "#ab69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
        };

        builder.Entity<Language>().HasData(languages);

        // courses type + category + level seeding can be added here similarly

        List<LevelCursus> levelCursuses = new()
        {
            new LevelCursus
            {
                Id = HardCode.LEVEL_ALL,
                Name = nameof(CursusLevelEnum.All),
                Translations = new() { ["fr"] = "Tous niveaux", ["en"] = "All levels" },
                Color = "#ff69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new LevelCursus
            {
                Id = HardCode.LEVEL_BEGINNER,
                Name = nameof(CursusLevelEnum.Beginner),
                Translations = new() { ["fr"] = "Débutant", ["en"] = "Beginner" },
                Color = "#fa69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new LevelCursus
            {
                Id = HardCode.LEVEL_INTERMEDIATE,
                Name = nameof(CursusLevelEnum.Intermediate),
                Translations = new() { ["fr"] = "Intermédiaire", ["en"] = "Intermediate" },
                Color = "#ab69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new LevelCursus
            {
                Id = HardCode.LEVEL_ADVANCED,
                Name = nameof(CursusLevelEnum.Advanced),
                Translations = new() { ["fr"] = "Avancé", ["en"] = "Advanced" },
                Color = "#ab69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
        };
        builder.Entity<LevelCursus>().HasData(levelCursuses);

        List<CategoryCursus> categoryCursuses = new()
        {
            new CategoryCursus
            {
                Id = HardCode.CATEGORY_BACK,
                Name = nameof(CursusCategoryEnum.Back),
                Translations = new() { ["fr"] = "Back-end", ["en"] = "Back-end" },
                Color = "#ff69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new CategoryCursus
            {
                Id = HardCode.CATEGORY_FRONT,
                Name = nameof(CursusCategoryEnum.Front),
                Translations = new() { ["fr"] = "Front-end", ["en"] = "Front-end" },
                Color = "#fa69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new CategoryCursus
            {
                Id = HardCode.CATEGORY_TECHNICS,
                Name = nameof(CursusCategoryEnum.Technics),
                Translations = new() { ["fr"] = "Technique", ["en"] = "Technical" },
                Color = "#ab69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new CategoryCursus
            {
                Id = HardCode.CATEGORY_SOFT,
                Name = nameof(CursusCategoryEnum.Soft),
                Translations = new() { ["fr"] = "Savoir-être", ["en"] = "Soft skills" },
                Color = "#ab69b4",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
        };
        builder.Entity<CategoryCursus>().HasData(categoryCursuses);

        // type slots
        List<TypeSlot> typeSlots = new()
        {
            new TypeSlot
            {
                Id = HardCode.TYPE_SLOT_PRESENTIAL,
                Name = nameof(SlotTypeEnum.Presential),
                Translations = new() { ["fr"] = "Présentiel", ["en"] = "In-person" },
                Color = "#ff69b4",
                Icon = "pi pi-arrow-down-left-and-arrow-up-right-to-center",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new TypeSlot
            {
                Id = HardCode.TYPE_SLOT_VISIO,
                Name = nameof(SlotTypeEnum.Visio),
                Translations = new() { ["fr"] = "Visioconférence", ["en"] = "Video call" },
                Color = "#aa69b4",
                Icon = "pi pi-desktop",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new TypeSlot
            {
                Id = HardCode.TYPE_SLOT_ALL,
                Name = nameof(SlotTypeEnum.All),
                Translations = new() { ["fr"] = "Tous types", ["en"] = "All types" },
                Color = "#1169b4",
                Icon = "pi pi-crown",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
        };
        builder.Entity<TypeSlot>().HasData(typeSlots);

        // privacy document types
        List<PrivacyDocumentType> documentTypes = new()
        {
            new PrivacyDocumentType
            {
                Id = HardCode.DOCUMENT_SIRET,
                Name = nameof(DocumentTypeEnum.CompanyRegistration),
                DisplayName = "K-Bis",
                Translations = new() { ["fr"] = "K-Bis", ["en"] = "Company registration (K-Bis)" },
                Color = "#ff69b4",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new PrivacyDocumentType
            {
                Id = HardCode.DOCUMENT_DIPLOME,
                Name = nameof(DocumentTypeEnum.Diploma),
                DisplayName = "Diplôme",
                Translations = new() { ["fr"] = "Diplôme", ["en"] = "Diploma" },
                Color = "#aa69b4",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new PrivacyDocumentType
            {
                Id = HardCode.DOCUMENT_ID,
                Name = nameof(DocumentTypeEnum.Identification),
                DisplayName = "Pièce d'identité",
                Translations = new() { ["fr"] = "Pièce d'identité", ["en"] = "ID document" },
                Color = "#1169b4",
                IsRequiredByDefault = true,
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new PrivacyDocumentType
            {
                Id = HardCode.DOCUMENT_PROOF_OF_ADDRESS,
                Name = nameof(DocumentTypeEnum.ProofOfAddress),
                DisplayName = "Attestation de domicile",
                Translations = new() { ["fr"] = "Attestation de domicile", ["en"] = "Proof of address" },
                Color = "#4a90d9",
                IsRequiredByDefault = true,
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new PrivacyDocumentType
            {
                Id = HardCode.DOCUMENT_OTHER,
                Name = nameof(DocumentTypeEnum.Other),
                DisplayName = "Autre",
                Translations = new() { ["fr"] = "Autre", ["en"] = "Other" },
                Color = "#1169b4",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
        };
        builder.Entity<PrivacyDocumentType>().HasData(documentTypes);

        // type payment methods
        List<PaymentMethod> paymentMethods = new()
        {
            new PaymentMethod
            {
                Id = HardCode.PAYMENT_METHOD_CARD,
                Name = nameof(EnumMethodPayment.Card),
                Translations = new() { ["fr"] = "Carte bancaire", ["en"] = "Card" },
                Color = "#ff69b4",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
        };
        builder.Entity<PaymentMethod>().HasData(paymentMethods);

        // seed dispute statuses (Feature 8)
        List<DisputeStatus> disputeStatuses = new()
        {
            new DisputeStatus
            {
                Id = HardCode.DISPUTE_STATUS_OPEN,
                Name = "Open",
                Translations = new() { ["fr"] = "Ouvert", ["en"] = "Open" },
                Color = "#ff3b30",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new DisputeStatus
            {
                Id = HardCode.DISPUTE_STATUS_RESOLVED,
                Name = "Resolved",
                Translations = new() { ["fr"] = "Résolu", ["en"] = "Resolved" },
                Color = "#34c759",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
        };
        builder.Entity<DisputeStatus>().HasData(disputeStatuses);

        // seed dispute outcomes (Feature 8)
        List<DisputeOutcome> disputeOutcomes = new()
        {
            new DisputeOutcome
            {
                Id = HardCode.DISPUTE_OUTCOME_TEACHER,
                Name = "Teacher",
                Translations = new() { ["fr"] = "En faveur du prof", ["en"] = "In favor of the teacher" },
                Color = "#0a84ff",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new DisputeOutcome
            {
                Id = HardCode.DISPUTE_OUTCOME_STUDENT,
                Name = "Student",
                Translations = new() { ["fr"] = "En faveur de l'élève", ["en"] = "In favor of the student" },
                Color = "#ff9500",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new DisputeOutcome
            {
                Id = HardCode.DISPUTE_OUTCOME_PARTIAL,
                Name = "Partial",
                Translations = new() { ["fr"] = "Partiel", ["en"] = "Partial" },
                Color = "#af52de",
                Icon = "",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
        };
        builder.Entity<DisputeOutcome>().HasData(disputeOutcomes);
    }
}
