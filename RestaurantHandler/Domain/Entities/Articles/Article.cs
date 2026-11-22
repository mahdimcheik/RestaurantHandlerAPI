using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantHandler.Models.Interfaces;

namespace RestaurantHandler.Models;

public enum ArticleStatus
{
    Draft,
    Published,
}

/// <summary>
/// Article scientifique rédigé par un admin. Titre, sous-titre et métadonnées restent en colonnes
/// (liste, tri, SEO) ; le corps (sections, références) vit dans <see cref="Content"/>, en jsonb.
/// Le <see cref="Slug"/> est figé dès la première publication : il porte l'URL indexée.
/// </summary>
[Table("Articles")]
public class Article : BaseModel
{
    [Required]
    [MaxLength(160)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>Code langue minuscule ("fr", "en") : un article n'existe que dans une langue.</summary>
    [Required]
    [MaxLength(8)]
    public string Language { get; set; } = "fr";

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Subtitle { get; set; }

    /// <summary>Chapeau en texte brut, affiché sous le titre.</summary>
    [MaxLength(1000)]
    public string? Intro { get; set; }

    /// <summary>Résumé en texte brut, calculé à l'écriture : meta description et tuile de liste.</summary>
    [MaxLength(320)]
    public string? Excerpt { get; set; }

    [MaxLength(512)]
    public string? CoverImageKey { get; set; }

    [MaxLength(300)]
    public string? CoverImageAlt { get; set; }

    [Required]
    [MaxLength(128)]
    public string AuthorDisplayName { get; set; } = string.Empty;

    [ForeignKey(nameof(Author))]
    public Guid? AuthorId { get; set; }
    public UserApp? Author { get; set; }

    [Required]
    public ArticleStatus Status { get; set; } = ArticleStatus.Draft;

    [Column(TypeName = "timestamp with time zone")]
    public DateTimeOffset? PublishedAt { get; set; }

    public List<string> Tags { get; set; } = [];

    public ArticleContent Content { get; set; } = new();

    public List<ArticleComment> Comments { get; set; } = [];
}

public class ArticleContent
{
    public List<ArticleSection> Sections { get; set; } = [];
    public List<ArticleReference> References { get; set; } = [];
}

public class ArticleSection
{
    /// <summary>Ancre HTML de la section (id du <c>&lt;section&gt;</c>), unique dans l'article.</summary>
    public string Anchor { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    /// <summary>Texte enrichi nettoyé par <c>RichTextSanitizer</c>.</summary>
    public string? Html { get; set; }
    public string? ImageKey { get; set; }
    public string? ImageAlt { get; set; }
    public string? ImageCaption { get; set; }
}

public class ArticleReference
{
    public string Label { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Doi { get; set; }
}
