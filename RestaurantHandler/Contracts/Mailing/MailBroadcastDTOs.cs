using System.ComponentModel.DataAnnotations;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Models;

/// <summary>Public visé par un admin ; plusieurs peuvent être cochés.</summary>
public enum MailAudience
{
    Teachers,
    Students,
    Admins,
}

public class AdminMailBroadcastCreate
{
    [Required]
    [MinLength(1, ErrorMessage = "Choisissez au moins un public")]
    public required List<MailAudience> Audiences { get; set; }

    [Required]
    [StringLength(HardCode.MAIL_BROADCAST_SUBJECT_MAX_LENGTH)]
    public required string Subject { get; set; }

    /// <summary>HTML de l'éditeur riche, nettoyé côté API.</summary>
    [Required]
    public required string Message { get; set; }
}

public class AdminMailAudienceCount
{
    [Required]
    public required List<MailAudience> Audiences { get; set; }
}

public class MailBroadcastSent
{
    public Guid Id { get; set; }
    public int RecipientCount { get; set; }
}

public class MailBroadcastSummary
{
    public Guid Id { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = "";
    public MailBroadcastSenderKind SenderKind { get; set; }
    public List<MailAudience> Audiences { get; set; } = [];
    public string Subject { get; set; } = "";
    public string BodyHtml { get; set; } = "";
    public int RecipientCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
