using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantHandler.Models.Interfaces;

namespace RestaurantHandler.Models;

public class Conversation : BaseModel
{
    [MaxLength(512,  ErrorMessage = "longueur maximal ne dois pas depasser le 512 caractères.")]
    public string Content { get; set; }
    [ForeignKey(nameof(Reservation))]
    public Guid ReservationId { get; set; }
    public Reservation? Reservation { get; set; }
    
    [ForeignKey(nameof(Sender))]
    public Guid SenderId { get; set; }
    public UserApp? Sender { get; set; }

    public ConversationAttachment? Attachment { get; set; }
}