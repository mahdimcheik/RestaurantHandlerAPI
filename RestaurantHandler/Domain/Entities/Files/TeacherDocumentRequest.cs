using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantHandler.Models.Interfaces;

namespace RestaurantHandler.Models.Files
{
    /// <summary>
    /// Document attendu d'UN prof spécifique (Feature 10) : soit demandé au cas par cas par un
    /// admin, soit créé automatiquement à la création du compte pour chaque
    /// <see cref="PrivacyDocumentType.IsRequiredByDefault"/> (voir
    /// <see cref="TeacherVerificationHelper.CreateDefaultDocumentRequestsAsync"/>) — dans ce
    /// second cas <see cref="RequestedBy"/> est <see langword="null"/>, ce qui distingue les deux
    /// origines pour l'affichage. <see cref="BaseModel.ArchivedAt"/> sert de statut « clôturée » :
    /// posé quand le prof envoie un document du type demandé, ou quand l'admin annule la demande.
    /// </summary>
    public class TeacherDocumentRequest : BaseModel
    {
        [Required]
        [ForeignKey(nameof(Teacher))]
        public Guid TeacherId { get; set; }
        public Teacher? Teacher { get; set; }

        [Required]
        [ForeignKey(nameof(Type))]
        public Guid TypeId { get; set; }
        public PrivacyDocumentType? Type { get; set; }

        /// <summary>L'admin à l'origine de la demande, ou <see langword="null"/> si générée
        /// automatiquement à la création du compte pour un type requis par défaut.</summary>
        [ForeignKey(nameof(RequestedBy))]
        public Guid? RequestedById { get; set; }
        public UserApp? RequestedBy { get; set; }

        /// <summary>Message libre de l'admin (pourquoi ce document est demandé).</summary>
        [StringLength(512)]
        public string? Note { get; set; }
    }
}
