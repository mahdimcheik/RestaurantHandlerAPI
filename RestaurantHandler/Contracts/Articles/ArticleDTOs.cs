using System.ComponentModel.DataAnnotations;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Http;

namespace RestaurantHandler.Models;

public class ArticleSectionInput
{
    [Required(ErrorMessage = "Le titre de la section est requis")]
    [StringLength(HardCode.ARTICLE_SECTION_TITLE_MAX_LENGTH)]
    public string Title { get; set; } = string.Empty;

    [StringLength(HardCode.ARTICLE_SECTION_MAX_HTML_LENGTH)]
    public string? Html { get; set; }

    [StringLength(512)]
    public string? ImageKey { get; set; }

    [StringLength(300)]
    public string? ImageAlt { get; set; }

    [StringLength(300)]
    public string? ImageCaption { get; set; }
}

public class ArticleReferenceInput
{
    [Required(ErrorMessage = "Le libellé de la référence est requis")]
    [StringLength(1000)]
    public string Label { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Url { get; set; }

    [StringLength(200)]
    public string? Doi { get; set; }
}

/// <summary>Création et mise à jour d'un article. Un brouillon peut être incomplet, pas un article publié.</summary>
public class ArticleUpsert
{
    [Required(ErrorMessage = "La langue est requise")]
    [StringLength(8)]
    public string Language { get; set; } = "fr";

    [Required(ErrorMessage = "Le titre est requis")]
    [StringLength(HardCode.ARTICLE_TITLE_MAX_LENGTH)]
    public string Title { get; set; } = string.Empty;

    [StringLength(HardCode.ARTICLE_SUBTITLE_MAX_LENGTH)]
    public string? Subtitle { get; set; }

    [StringLength(HardCode.ARTICLE_INTRO_MAX_LENGTH)]
    public string? Intro { get; set; }

    /// <summary>Calculé depuis le titre s'il est vide ; ignoré une fois l'article publié.</summary>
    [StringLength(120)]
    public string? Slug { get; set; }

    /// <summary>Ignoré pour un éditeur : son article est signé de son nom. Requis pour un admin.</summary>
    [StringLength(128)]
    public string AuthorDisplayName { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    [StringLength(512)]
    public string? CoverImageKey { get; set; }

    [StringLength(300)]
    public string? CoverImageAlt { get; set; }

    public List<ArticleSectionInput> Sections { get; set; } = [];

    public List<ArticleReferenceInput> References { get; set; } = [];
}

public class ArticleSectionDetails
{
    public string Anchor { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Html { get; set; }
    public string? ImageKey { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageAlt { get; set; }
    public string? ImageCaption { get; set; }
}

public class ArticleReferenceDetails
{
    public string Label { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Doi { get; set; }
}

/// <summary>Tuile de liste. Les clés d'image sont résolues en URL par le service.</summary>
public class ArticleSummary
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Language { get; set; } = "fr";
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Excerpt { get; set; }
    public string? CoverImageKey { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? CoverImageAlt { get; set; }
    public string AuthorDisplayName { get; set; } = string.Empty;

    /// <summary>Profil public de l'auteur quand c'est un prof ; null pour un article d'admin.</summary>
    public Guid? AuthorProfileId { get; set; }
    public List<string> Tags { get; set; } = [];
    public ArticleStatus Status { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int CommentsCount { get; set; }

    public ArticleSummary() { }

    public ArticleSummary(Article article, int commentsCount = 0)
    {
        Id = article.Id;
        Slug = article.Slug;
        Language = article.Language;
        Title = article.Title;
        Subtitle = article.Subtitle;
        Excerpt = article.Excerpt;
        CoverImageKey = article.CoverImageKey;
        CoverImageAlt = article.CoverImageAlt;
        AuthorDisplayName = article.AuthorDisplayName;
        Tags = [.. article.Tags];
        Status = article.Status;
        PublishedAt = article.PublishedAt;
        CreatedAt = article.CreatedAt;
        UpdatedAt = article.UpdatedAt;
        CommentsCount = commentsCount;
    }
}

/// <summary>Encadré « à propos de l'auteur » d'un article de prof, repris de son profil public.</summary>
public class ArticleAuthor
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Title { get; set; }
    /// <summary>Description du profil, nettoyée par <c>RichTextSanitizer</c>.</summary>
    public string? DescriptionHtml { get; set; }
    public string? ImgUrl { get; set; }
}

public class ArticleDetails : ArticleSummary
{
    public string? Intro { get; set; }

    /// <summary>Renseigné sur la page publique quand l'auteur est un prof ; null sinon.</summary>
    public ArticleAuthor? Author { get; set; }
    public List<ArticleSectionDetails> Sections { get; set; } = [];
    public List<ArticleReferenceDetails> References { get; set; } = [];

    public ArticleDetails() { }

    public ArticleDetails(Article article, int commentsCount = 0)
        : base(article, commentsCount)
    {
        Intro = article.Intro;
        Sections = article
            .Content.Sections.Select(s => new ArticleSectionDetails
            {
                Anchor = s.Anchor,
                Title = s.Title,
                Html = s.Html,
                ImageKey = s.ImageKey,
                ImageAlt = s.ImageAlt,
                ImageCaption = s.ImageCaption,
            })
            .ToList();
        References = article
            .Content.References.Select(r => new ArticleReferenceDetails
            {
                Label = r.Label,
                Url = r.Url,
                Doi = r.Doi,
            })
            .ToList();
    }
}

/// <summary>État de table PrimeNG, plus les filtres propres à la liste publique.</summary>
public class ArticleQuery : CustomTableState
{
    public string? Language { get; set; }
    public string? Tag { get; set; }
}

public class ArticleSitemapEntry
{
    public string Slug { get; set; } = string.Empty;
    public DateTimeOffset LastModified { get; set; }
}

public class ArticleImageUpload
{
    [Required]
    public IFormFile File { get; set; } = null!;

    /// <summary>Image de couverture : recadrée au format de partage (1200×630).</summary>
    public bool Cover { get; set; }
}

public class ArticleImage
{
    public string Key { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class ArticleCommentCreate
{
    [Required(ErrorMessage = "Le commentaire est requis")]
    [StringLength(HardCode.ARTICLE_COMMENT_MAX_LENGTH, ErrorMessage = "Le commentaire ne peut dépasser 2000 caractères")]
    public string Content { get; set; } = string.Empty;

    /// <summary>Commentaire racine auquel on répond ; jamais une réponse.</summary>
    public Guid? ParentId { get; set; }
}

public class ArticleCommentDetails
{
    public Guid Id { get; set; }
    public Guid ArticleId { get; set; }
    public Guid? ParentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public Guid AuthorId { get; set; }
    public string AuthorFirstName { get; set; } = string.Empty;
    public string AuthorLastName { get; set; } = string.Empty;
    public string? AuthorImgUrl { get; set; }
    public List<ArticleCommentDetails> Replies { get; set; } = [];

    public ArticleCommentDetails() { }

    public ArticleCommentDetails(ArticleComment comment)
    {
        Id = comment.Id;
        ArticleId = comment.ArticleId;
        ParentId = comment.ParentId;
        Content = comment.Content;
        CreatedAt = comment.CreatedAt;
        AuthorId = comment.UserId;
        if (comment.User is not null)
        {
            AuthorFirstName = comment.User.FirstName;
            AuthorLastName = comment.User.LastName;
            AuthorImgUrl = comment.User.ImgUrl;
        }
    }
}
