using RestaurantHandler.Utilities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantHandler.Models.Files
{
    public class PrivacyDocument : Document
    {
        [Required]
        public required string Title { get; set; }
        public string?  Description { get; set; }
        [ForeignKey(nameof(Type))]
        public Guid TypeId { get; set; }
        public PrivacyDocumentType Type { get; set; }

        /// <summary>Cycle de revue admin. À l'envoi : <see cref="DocumentReviewStatusEnum.Pending"/>.</summary>
        [Required]
        public DocumentReviewStatusEnum Status { get; set; } = DocumentReviewStatusEnum.Pending;

        /// <summary>Admin ayant validé/refusé. <c>null</c> tant qu'en attente.</summary>
        [ForeignKey(nameof(ReviewedBy))]
        public Guid? ReviewedById { get; set; }
        public UserApp? ReviewedBy { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTimeOffset? ReviewedAt { get; set; }

        /// <summary>Motif obligatoire quand <see cref="Status"/> == Refused.</summary>
        [StringLength(512)]
        public string? RefusalReason { get; set; }

        /// <summary>Date de suppression du fichier en stockage, une fois la durée de conservation
        /// écoulée (voir <c>JobChron.PurgeExpiredPrivacyDocuments</c>). La ligne reste en base pour
        /// garder la trace de la revue.</summary>
        [Column(TypeName = "timestamp with time zone")]
        public DateTimeOffset? FilePurgedAt { get; set; }
    }
}
