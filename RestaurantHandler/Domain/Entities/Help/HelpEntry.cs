using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantHandler.Models.Interfaces;

namespace RestaurantHandler.Models;

public enum HelpEntryStatus
{
    Draft,
    Published,
}

/// <summary>Rubrique du centre d'aide ; les libellés sont traduits côté Angular.</summary>
public enum HelpCategory
{
    GettingStarted,
    Account,
    Profile,
    Search,
    Documents,
    Other,
}

/// <summary>Public visé par une fiche : <see cref="All"/> s'affiche pour les profs comme pour les élèves.</summary>
public enum HelpAudience
{
    All,
    Teacher,
    Student,
}

/// <summary>
/// Fiche du centre d'aide (question, réponse en texte enrichi, vidéo YouTube facultative), rédigée
/// par un admin. Comme un article, elle n'existe que dans une langue et son <see cref="Slug"/> est
/// figé dès la première publication (cf. ADR-0019).
/// </summary>
[Table("HelpEntries")]
public class HelpEntry : BaseModel
{
    [Required]
    [MaxLength(160)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>Code langue minuscule ("fr", "en").</summary>
    [Required]
    [MaxLength(8)]
    public string Language { get; set; } = "fr";

    [Required]
    [MaxLength(200)]
    public string Question { get; set; } = string.Empty;

    /// <summary>Réponse courte en texte brut : liste, résultats de recherche et meta description.</summary>
    [MaxLength(300)]
    public string? Summary { get; set; }

    /// <summary>Réponse complète, nettoyée par <c>RichTextSanitizer</c>.</summary>
    public string? AnswerHtml { get; set; }

    [Required]
    public HelpCategory Category { get; set; } = HelpCategory.Other;

    [Required]
    public HelpAudience Audience { get; set; } = HelpAudience.All;

    /// <summary>Synonymes cherchables, absents du texte affiché.</summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>Identifiant de vidéo YouTube (11 caractères), extrait de l'URL saisie par l'admin.</summary>
    [MaxLength(11)]
    public string? YoutubeVideoId { get; set; }

    /// <summary>Ordre dans la rubrique, croissant.</summary>
    public int SortOrder { get; set; }

    [Required]
    public HelpEntryStatus Status { get; set; } = HelpEntryStatus.Draft;

    [Column(TypeName = "timestamp with time zone")]
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>
    /// Question, résumé, mots-clés et texte de la réponse, en minuscules et sans accents : calculé à
    /// l'écriture, c'est la seule colonne que lit la recherche.
    /// </summary>
    [Required]
    public string SearchText { get; set; } = string.Empty;
}
