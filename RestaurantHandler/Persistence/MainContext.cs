using RestaurantHandler.Models;
using RestaurantHandler.Models.Files;
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
    public DbSet<Gender> Genders { get; set; }

    // Profile entities
    public DbSet<Teacher> Teachers { get; set; }
    public DbSet<Student> Students { get; set; }

    // Related entities
    public DbSet<Experience> Experiences { get; set; }
    public DbSet<Formation> Formations { get; set; }

    // document & type
    public DbSet<Document> Documents { get; set; }
    public DbSet<PrivacyDocument> PrivacyDocuments { get; set; }
    public DbSet<PrivacyDocumentType> PrivacyDocumentTypes { get; set; }
    public DbSet<PrivacyDocumentAccessLog> PrivacyDocumentAccessLogs { get; set; } // consultations admin des justificatifs (RGPD)
    public DbSet<TeacherDocumentRequest> TeacherDocumentRequests { get; set; } // demande ad hoc d'un document supplémentaire à UN prof (Feature 10)

    // notifications
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<WebPushSubscription> WebPushSubscriptions { get; set; } // un par appareil abonné aux notifications natives

    // favorites
    public DbSet<Favorite> Favorites { get; set; }

    // centre d'aide
    public DbSet<HelpEntry> HelpEntries { get; set; }

    // Envois groupés par e-mail (ADR-0023)
    public DbSet<MailBroadcast> MailBroadcasts { get; set; }
    public DbSet<MailBroadcastRecipient> MailBroadcastRecipients { get; set; }

    // paramètres de la plateforme (ligne unique)
    public DbSet<PlatformSettings> PlatformSettings { get; set; }

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
