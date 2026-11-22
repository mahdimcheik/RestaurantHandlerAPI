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
    }
}
