using System.ComponentModel.DataAnnotations;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Models;

/// <summary>Création et mise à jour d'une fiche d'aide.</summary>
public class HelpUpsert
{
    [Required(ErrorMessage = "La langue est requise")]
    [StringLength(8)]
    public string Language { get; set; } = "fr";

    [Required(ErrorMessage = "La question est requise")]
    [StringLength(HardCode.HELP_QUESTION_MAX_LENGTH)]
    public string Question { get; set; } = string.Empty;

    /// <summary>Calculé depuis la question s'il est vide ; ignoré une fois la fiche publiée.</summary>
    [StringLength(120)]
    public string? Slug { get; set; }

    [StringLength(HardCode.HELP_SUMMARY_MAX_LENGTH)]
    public string? Summary { get; set; }

    [StringLength(HardCode.HELP_ANSWER_MAX_HTML_LENGTH)]
    public string? AnswerHtml { get; set; }

    public HelpCategory Category { get; set; } = HelpCategory.Other;

    public HelpAudience Audience { get; set; } = HelpAudience.All;

    public List<string> Keywords { get; set; } = [];

    /// <summary>Lien YouTube (youtu.be, watch?v=, /embed/, /shorts/) ou identifiant seul.</summary>
    [StringLength(300)]
    public string? YoutubeUrl { get; set; }

    [Range(0, 10_000)]
    public int SortOrder { get; set; }
}

public class HelpSummary
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Language { get; set; } = "fr";
    public string Question { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public HelpCategory Category { get; set; }
    public HelpAudience Audience { get; set; }
    public bool HasVideo { get; set; }
    public int SortOrder { get; set; }
    public HelpEntryStatus Status { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public HelpSummary() { }

    public HelpSummary(HelpEntry entry)
    {
        Id = entry.Id;
        Slug = entry.Slug;
        Language = entry.Language;
        Question = entry.Question;
        Summary = entry.Summary;
        Category = entry.Category;
        Audience = entry.Audience;
        HasVideo = entry.YoutubeVideoId is not null;
        SortOrder = entry.SortOrder;
        Status = entry.Status;
        PublishedAt = entry.PublishedAt;
        CreatedAt = entry.CreatedAt;
        UpdatedAt = entry.UpdatedAt;
    }
}

public class HelpDetails : HelpSummary
{
    public string? AnswerHtml { get; set; }
    public List<string> Keywords { get; set; } = [];
    public string? YoutubeVideoId { get; set; }

    /// <summary>Autres fiches publiées de la même rubrique et de la même langue.</summary>
    public List<HelpSummary> Related { get; set; } = [];

    public HelpDetails() { }

    public HelpDetails(HelpEntry entry)
        : base(entry)
    {
        AnswerHtml = entry.AnswerHtml;
        Keywords = [.. entry.Keywords];
        YoutubeVideoId = entry.YoutubeVideoId;
    }
}

public class HelpSearchQuery
{
    [Required]
    [StringLength(200)]
    public string Q { get; set; } = string.Empty;

    [StringLength(8)]
    public string? Language { get; set; }

    /// <summary>Filtre de public ; les fiches <see cref="HelpAudience.All"/> restent toujours visibles.</summary>
    public HelpAudience? Audience { get; set; }
}

public class HelpAdminQuery : CustomTableState
{
    public string? Language { get; set; }
    public HelpCategory? Category { get; set; }
}

public class HelpSitemapEntry
{
    public string Slug { get; set; } = string.Empty;
    public DateTimeOffset LastModified { get; set; }
}
