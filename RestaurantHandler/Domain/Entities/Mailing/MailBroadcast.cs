using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantHandler.Models;

/// <summary>Publics qu'un admin peut viser, combinables.</summary>
[Flags]
public enum MailBroadcastAudience
{
    None = 0,
    Teachers = 1,
    Students = 2,
    Admins = 4,
    Everyone = Teachers | Students | Admins,
}

/// <summary>Qui a écrit : un admin à un ou plusieurs publics, ou un prof à ses élèves.</summary>
public enum MailBroadcastSenderKind
{
    Admin,
    Teacher,
}

/// <summary>
/// Envoi groupé par e-mail (ADR-0023). Conservé pour l'historique et l'audit : un mail part par
/// destinataire (<see cref="Recipients"/>), avec l'adresse de l'expéditeur en Reply-To.
/// </summary>
[Table("MailBroadcasts")]
public class MailBroadcast
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [ForeignKey(nameof(Sender))]
    public Guid SenderId { get; set; }
    public UserApp? Sender { get; set; }

    public MailBroadcastSenderKind SenderKind { get; set; }

    /// <summary><see cref="MailBroadcastAudience.None"/> pour un envoi de prof.</summary>
    public MailBroadcastAudience Audience { get; set; }

    [Required]
    [MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string BodyHtml { get; set; } = string.Empty;

    public int RecipientCount { get; set; }

    [Column(TypeName = "timestamp with time zone")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<MailBroadcastRecipient> Recipients { get; set; } = new List<MailBroadcastRecipient>();
}

[Table("MailBroadcastRecipients")]
public class MailBroadcastRecipient
{
    public Guid BroadcastId { get; set; }
    public MailBroadcast? Broadcast { get; set; }

    public Guid UserId { get; set; }
    public UserApp? User { get; set; }
}
