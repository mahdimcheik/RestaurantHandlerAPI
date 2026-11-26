using RestaurantHandler.Models;
using RestaurantHandler.Models.Interfaces;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using RestaurantHandler.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace RestaurantHandler.Contexts;

public class MainContext : IdentityDbContext<UserApp, RoleApp, Guid>
{
    public DbSet<UserApp> Users { get; set; }
    public DbSet<RoleApp> Roles { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    // notifications
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<WebPushSubscription> WebPushSubscriptions { get; set; } // un par appareil abonné aux notifications natives

    // carte
    public DbSet<Category> Categories { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Ingredient> Ingredients { get; set; }
    public DbSet<ProductIngredient> ProductIngredients { get; set; }
    public DbSet<OptionGroup> OptionGroups { get; set; }
    public DbSet<OptionChoice> OptionChoices { get; set; }
    public DbSet<ProductOptionGroup> ProductOptionGroups { get; set; } // groupes proposés par produit, ordonnés
    public DbSet<Menu> Menus { get; set; }
    public DbSet<MenuSlot> MenuSlots { get; set; }
    public DbSet<MenuSlotOption> MenuSlotOptions { get; set; }

    // restaurant : horaires, fermetures, paramètres
    public DbSet<OpeningHours> OpeningHours { get; set; }
    public DbSet<Closure> Closures { get; set; }
    public DbSet<RestaurantSettings> RestaurantSettings { get; set; }
    public DbSet<RestaurantSettingsHistory> RestaurantSettingsHistory { get; set; }

    // commandes et paiements
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<OrderItemModification> OrderItemModifications { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<OrderStatusHistory> OrderStatusHistory { get; set; }

    // facturation électronique (ADR-0014)
    public DbSet<BusinessProfile> BusinessProfiles { get; set; }
    public DbSet<InvoiceRequest> InvoiceRequests { get; set; }

    public MainContext(DbContextOptions options)
        : base(options) { }

    /// <summary>
    /// Compare les dictionnaires jsonb par contenu. Sans lui, EF Core 10 les compare par
    /// référence : le seed <c>HasData</c> paraît modifié à chaque build, et
    /// <c>Database.Migrate()</c> refuse de démarrer (PendingModelChangesWarning).
    /// </summary>
    internal static readonly ValueComparer<Dictionary<string, string>> TranslationsComparer =
        new(
            (a, b) =>
                a == b
                || (a != null && b != null && a.Count == b.Count && !a.Except(b).Any()),
            d => d.Aggregate(0, (h, kv) => h ^ HashCode.Combine(kv.Key, kv.Value)),
            d => new Dictionary<string, string>(d)
        );

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Traductions (cf. CLAUDE.md Feature 14) : jsonb pour tout ce qui dérive de
        // TranslatableModelOption, configuré une seule fois par forme CLR plutôt que
        // répété par entité.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(TranslatableModelOption).IsAssignableFrom(entityType.ClrType))
            {
                builder
                    .Entity(entityType.ClrType)
                    .Property<Dictionary<string, string>>(nameof(TranslatableModelOption.Translations))
                    .HasColumnType("jsonb")
                    .Metadata.SetValueComparer(TranslationsComparer);
            }
        }

        builder.ApplyConfigurationsFromAssembly(typeof(MainContext).Assembly);
        ReferenceDataSeed.Apply(builder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder builder)
    {
        base.OnConfiguring(builder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveConversion<CustomDateTimeConversion>();
        base.ConfigureConventions(configurationBuilder);
    }
}
