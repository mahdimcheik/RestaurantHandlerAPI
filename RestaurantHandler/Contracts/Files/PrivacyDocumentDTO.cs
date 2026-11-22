using RestaurantHandler.Utilities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantHandler.Models.Files
{
    public class PrivacyDocumentDetails
    {
        [Required]
        public Guid Id { get; set; }

        [Required]
        public string FileName { get; set; } = string.Empty;

        [Required]
        public string FilePath { get; set; } = string.Empty;

        [Required]
        public string MimeType { get; set; } = string.Empty;

        public long Size { get; set; }
        [Required]
        public required string Title { get; set; }
        public string? Description { get; set; }
        public PrivacyDocumentTypeDetails Type { get; set; }
        public DocumentReviewStatusEnum Status { get; set; }
        public DateTimeOffset? ReviewedAt { get; set; }
        public string? RefusalReason { get; set; }

        /// <summary>Vrai quand le fichier a été supprimé (durée de conservation écoulée) :
        /// <see cref="FilePath"/> est alors vide.</summary>
        public bool Purged { get; set; }
    }
    /// <summary>Corps de <c>PATCH /UserManagement/documents/{id}/review</c>. <see cref="Status"/>
    /// ne doit valoir que Validated ou Refused (Pending refusé par le service) ;
    /// <see cref="RefusalReason"/> est obligatoire quand Refused (vérifié en service).</summary>
    public class DocumentReviewDecision
    {
        [Required]
        public DocumentReviewStatusEnum Status { get; set; }

        [StringLength(512)]
        public string? RefusalReason { get; set; }
    }
}
