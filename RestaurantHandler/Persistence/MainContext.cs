using RestaurantHandler.Models;
using RestaurantHandler.Models.Files;
using RestaurantHandler.Models.Interfaces;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using RestaurantHandler.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

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
    public DbSet<Address> Addresses { get; set; }
    public DbSet<TypeAddress> TypeAddresses { get; set; }
    public DbSet<Experience> Experiences { get; set; }
    public DbSet<Formation> Formations { get; set; }
    public DbSet<Language> Languages { get; set; }
    public DbSet<CategoryCursus> CategoryCursuses { get; set; }
    public DbSet<LevelCursus> LevelCursuses { get; set; }

    // reservations
    public DbSet<Slot> Slots { get; set; }
    public DbSet<TypeSlot> TypeSlots { get; set; }
    public DbSet<Reservation> Reservations { get; set; }
    public DbSet<ReservationStatusHistory> ReservationStatusHistories { get; set; }
    public DbSet<Dispute> Disputes { get; set; } // litige ouvert par l'élève sur une réservation
    public DbSet<DisputeStatus> DisputeStatuses { get; set; }
    public DbSet<DisputeOutcome> DisputeOutcomes { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<Cursus> Cursuses { get; set; }
    public DbSet<StatusOrder> StatusOrders { get; set; }
    public DbSet<StatusReservation> StatusReservations { get; set; }
    
    // conversations
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<ConversationAttachment> ConversationAttachments { get; set; }

    // payments and transactions
    public DbSet<Payment> Payments { get; set; } // paiement d'une commande
    public DbSet<Refund> Refunds { get; set; } // remboursement d'une réservation annulée (côté acheteur)
    public DbSet<TeacherPayout> TeacherPayouts { get; set; } // une ligne par professeur (les comptes des profs)
    public DbSet<TeacherWalletTransaction> TeacherWalletTransactions { get; set; } // historique des transactions du portefeuille, acomptes, retraits, remboursements
    public DbSet<TypeTeacherTransaction> TypeTransactions { get; set; } // types de transactions (acompte, retrait, remboursement)
    public DbSet<StatusTransaction> StatusTransactions { get; set; } // statuts des transactions (en attente, complété, échoué)
    public DbSet<PaymentMethod> PaymentMethods { get; set; } // méthodes de paiement (carte bancaire, PayPal, etc.)

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

    // suivi de cours
    public DbSet<Board> Boards { get; set; }
    public DbSet<BoardList> BoardLists { get; set; }
    public DbSet<BoardCard> BoardCards { get; set; }
    public DbSet<BoardCardAttachment> BoardCardAttachments { get; set; }

    // articles scientifiques
    public DbSet<Article> Articles { get; set; }
    public DbSet<ArticleComment> ArticleComments { get; set; }
    public DbSet<ArticleViewDay> ArticleViewDays { get; set; } // compteur d'affichages par jour

    // centre d'aide
    public DbSet<HelpEntry> HelpEntries { get; set; }

    // Envois groupés par e-mail (ADR-0023)
    public DbSet<MailBroadcast> MailBroadcasts { get; set; }
    public DbSet<MailBroadcastRecipient> MailBroadcastRecipients { get; set; }

    // paramètres de la plateforme (ligne unique)
    public DbSet<PlatformSettings> PlatformSettings { get; set; }

    public MainContext(DbContextOptions options)
        : base(options) { }

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
                    .HasColumnType("jsonb");
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
