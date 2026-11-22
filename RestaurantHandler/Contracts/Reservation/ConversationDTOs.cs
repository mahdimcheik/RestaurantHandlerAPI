using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models;

public class ConversationCreate
{
    [Required]
    [MaxLength(512, ErrorMessage = "longueur maximal ne dois pas depasser le 512 caractères.")]
    public string Content { get; set; } = string.Empty;

    [Required]
    public Guid ReservationId { get; set; }
    [Required]
    public Guid SenderId { get; set; }
}

public class ConversationDetails
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid ReservationId { get; set; }
    public Guid SenderId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Fichier joint au message (ADR-0016), avec un lien signé neuf ; absent pour un message texte.</summary>
    public ConversationAttachmentDetails? Attachment { get; set; }
}

public class ConversationAttachmentDetails
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public string MimeType { get; set; } = string.Empty;

    [Required]
    public long Size { get; set; }

    /// <summary>Lien signé, valable <c>SIGNED_URL_MINUTES</c> ; jamais persisté.</summary>
    public string? Url { get; set; }

    [Required]
    public DateTimeOffset UploadedAt { get; set; }
}

/// <summary>Corps <c>multipart/form-data</c> : un fichier et sa légende facultative.</summary>
public class ConversationAttachmentUpload
{
    [Required]
    public IFormFile File { get; set; } = null!;

    [MaxLength(512, ErrorMessage = "longueur maximal ne dois pas depasser le 512 caractères.")]
    public string? Content { get; set; }
}
