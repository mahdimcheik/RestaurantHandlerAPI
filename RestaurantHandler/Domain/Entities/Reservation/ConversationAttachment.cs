using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantHandler.Models;

/// <summary>
/// Fichier d'un message du chat de réservation (ADR-0016) : image ou PDF, servi par lien signé.
/// Le fil est immuable : une pièce jointe ne se supprime pas.
/// </summary>
public class ConversationAttachment : Document
{
    [Required]
    [ForeignKey(nameof(Conversation))]
    public Guid ConversationId { get; set; }

    public Conversation? Conversation { get; set; }
}
