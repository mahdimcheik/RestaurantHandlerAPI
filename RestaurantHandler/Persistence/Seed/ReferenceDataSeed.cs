using RestaurantHandler.Models;
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
                Id = HardCode.ROLE_MANAGER,
                Name = nameof(RoleEnum.Manager),
                NormalizedName = nameof(RoleEnum.Manager).ToUpper(),
                Color = "#3d82f2",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ConcurrencyStamp = "MANAGER-STAMP-2026",
            },
            new RoleApp
            {
                Id = HardCode.ROLE_CLIENT,
                Name = nameof(RoleEnum.Client),
                NormalizedName = nameof(RoleEnum.Client).ToUpper(),
                Color = "#31d68f",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ConcurrencyStamp = "CLIENT-STAMP-2026",
            },
        };
        builder.Entity<RoleApp>().HasData(roles);

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
    }
}
