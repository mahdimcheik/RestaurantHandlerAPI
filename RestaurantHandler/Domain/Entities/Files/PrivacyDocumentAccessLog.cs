using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantHandler.Models.Files
{
    /// <summary>
    /// Trace RGPD : une ligne par justificatif exposé à un admin (URL signée générée dans le
    /// dossier de vérification). Sert de preuve d'accès en cas de demande ou d'audit.
    /// </summary>
    public class PrivacyDocumentAccessLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [ForeignKey(nameof(Document))]
        public Guid DocumentId { get; set; }
        public PrivacyDocument? Document { get; set; }

        [ForeignKey(nameof(Admin))]
        public Guid AdminId { get; set; }
        public UserApp? Admin { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTimeOffset AccessedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
