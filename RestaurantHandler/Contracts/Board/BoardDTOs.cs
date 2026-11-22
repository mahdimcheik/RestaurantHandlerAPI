using System.ComponentModel.DataAnnotations;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Models;

/// <summary>Élève ou professeur d'un tableau, réduit à ce que l'écran affiche.</summary>
public class BoardPerson
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    public string? ImgUrl { get; set; }
}

/// <summary>Un élève du professeur : au moins une réservation commune (US-E20-01).</summary>
public class MyStudentDetails : BoardPerson
{
    [Required]
    public int ActiveBoardCount { get; set; }
}

public class BoardCreate
{
    [Required]
    public Guid StudentId { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MaxLength(HardCode.BOARD_TITLE_MAX_LENGTH)]
    public string Title { get; set; } = string.Empty;
}

public class BoardUpdate
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(HardCode.BOARD_TITLE_MAX_LENGTH)]
    public string Title { get; set; } = string.Empty;
}

/// <summary>Ligne de la barre latérale des tableaux.</summary>
public class BoardSummary
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public BoardPerson Student { get; set; } = new();

    [Required]
    public BoardPerson Teacher { get; set; } = new();

    [Required]
    public int ActiveCardCount { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }
}

/// <summary>Tableau complet, non paginé : listes et cartes actives, dans l'ordre.</summary>
public class BoardDetails
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public BoardPerson Student { get; set; } = new();

    [Required]
    public BoardPerson Teacher { get; set; } = new();

    public DateTimeOffset? UpdatedAt { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }

    [Required]
    public List<BoardListDetails> Lists { get; set; } = [];
}

public class BoardListDetails
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public int Position { get; set; }

    [Required]
    public List<BoardCardTile> Cards { get; set; } = [];
}

public class BoardListCreate
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(HardCode.BOARD_TITLE_MAX_LENGTH)]
    public string Title { get; set; } = string.Empty;
}

public class BoardListUpdate
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(HardCode.BOARD_TITLE_MAX_LENGTH)]
    public string Title { get; set; } = string.Empty;
}

/// <summary>Ordre complet des listes du tableau, chacune une fois.</summary>
public class BoardListOrder
{
    [Required]
    [MinLength(1)]
    public List<Guid> ListIds { get; set; } = [];
}

/// <summary>Tuile d'une carte : pas de description, seulement un extrait.</summary>
public class BoardCardTile
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid BoardListId { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public BoardCardImportance Importance { get; set; }

    [Required]
    public int Position { get; set; }

    public Guid? CursusId { get; set; }

    public string? CursusName { get; set; }

    public Guid? ReservationId { get; set; }

    public DateTimeOffset? ReservationDateFrom { get; set; }

    [Required]
    public bool HasDescription { get; set; }

    public string? Excerpt { get; set; }

    [Required]
    public int AttachmentCount { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }

    /// <summary>Nul : carte cachée à l'élève.</summary>
    public DateTimeOffset? SharedAt { get; set; }
}

public class BoardCardDetails : BoardCardTile
{
    /// <summary>HTML nettoyé à l'écriture ; le client le nettoie de nouveau à l'affichage.</summary>
    public string? Description { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Dans l'ordre d'envoi ; chaque <c>Url</c> est un lien signé à durée limitée.</summary>
    [Required]
    public List<BoardCardAttachmentDetails> Attachments { get; set; } = [];
}

public class BoardCardAttachmentDetails
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

/// <summary>Corps <c>multipart/form-data</c> : un fichier par envoi.</summary>
public class BoardCardAttachmentUpload
{
    [Required]
    public IFormFile File { get; set; } = null!;
}

public class BoardCardCreate
{
    [Required]
    public Guid BoardListId { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MaxLength(HardCode.CARD_TITLE_MAX_LENGTH)]
    public string Title { get; set; } = string.Empty;

    public BoardCardImportance Importance { get; set; } = BoardCardImportance.Normale;

    public Guid? CursusId { get; set; }

    public Guid? ReservationId { get; set; }

    public string? Description { get; set; }
}

/// <summary>Remplacement complet : un cursus ou une séance absents sont retirés.</summary>
public class BoardCardUpdate
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(HardCode.CARD_TITLE_MAX_LENGTH)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public BoardCardImportance Importance { get; set; }

    public Guid? CursusId { get; set; }

    public Guid? ReservationId { get; set; }

    public string? Description { get; set; }
}

/// <summary>Intention de déplacement : une position au-delà de la fin place la carte en dernier.</summary>
public class BoardCardMove
{
    [Required]
    public Guid BoardListId { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int Position { get; set; }
}
