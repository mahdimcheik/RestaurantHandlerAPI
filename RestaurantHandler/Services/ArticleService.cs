using RestaurantHandler.Contexts;
using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace RestaurantHandler.Services;

/// <summary>
/// Articles scientifiques : écriture par les admins et par les profs éditeurs (leurs articles seulement), lecture publique des seuls articles
/// publiés. Le HTML des sections est nettoyé à l'écriture (ADR-0014), le corps est stocké en
/// jsonb et les images vivent dans le dossier public <see cref="StorageService.ArticleFolder"/>.
/// </summary>
public class ArticleService(
    MainContext context,
    RichTextSanitizer sanitizer,
    StorageService storage,
    HybridCache cache,
    ILogger<ArticleService> logger
)
{
    public sealed class ArticlePage
    {
        public List<ArticleSummary> Items { get; set; } = [];
        public int Total { get; set; }
    }

    // ── Lecture publique ────────────────────────────────────────────────────────────────

    public async Task<Response<List<ArticleSummary>>> GetPublishedAsync(ArticleQuery query)
    {
        var language = NormalizeLanguage(query.Language);
        var tag = string.IsNullOrWhiteSpace(query.Tag) ? null : query.Tag.Trim();
        var rows = query.Rows is > 0 and <= 50 ? query.Rows : 12;
        var first = Math.Max(0, query.First);
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        async ValueTask<ArticlePage> Load(CancellationToken ct)
        {
            var published = Published();
            if (language is not null)
                published = published.Where(a => a.Language == language);
            if (tag is not null)
                published = published.Where(a => a.Tags.Contains(tag));
            if (search is not null)
                published = published.Where(a => EF.Functions.ILike(a.Title, $"%{EscapeLike(search)}%"));

            var total = await published.CountAsync(ct);
            var items = await published
                .OrderByDescending(a => a.PublishedAt)
                .ThenBy(a => a.Id)
                .Skip(first)
                .Take(rows)
                .Select(ToRow)
                .ToListAsync(ct);

            return new ArticlePage
            {
                Total = total,
                Items = items.Select(Summary).ToList(),
            };
        }

        // Une recherche libre multiplierait les clés : seules les pages de navigation sont gardées.
        var page = search is null
            ? await cache.GetOrCreateAsync(
                CacheKeys.ArticleList(language, tag, first, rows),
                Load,
                CacheKeys.Articles,
                [CacheKeys.ARTICLES_TAG]
            )
            : await Load(CancellationToken.None);

        return new Response<List<ArticleSummary>>
        {
            Status = 200,
            Message = "Articles récupérés avec succès",
            Data = page.Items,
            Count = page.Total,
        };
    }

    public async Task<Response<ArticleDetails>> GetPublishedBySlugAsync(string slug)
    {
        var normalized = (slug ?? string.Empty).Trim().ToLowerInvariant();
        var details = await cache.GetOrCreateAsync(
            CacheKeys.ArticleBySlug(normalized),
            async ct =>
            {
                var found = await Published()
                    .Where(a => a.Slug == normalized)
                    .Select(ToRow)
                    .FirstOrDefaultAsync(ct);
                return found is null ? null : Details(found);
            },
            CacheKeys.Articles,
            [CacheKeys.ARTICLES_TAG]
        );

        if (details is null)
            return NotFound<ArticleDetails>();

        // Hors du cache de l'article : une modification du profil se voit tout de suite.
        if (details.AuthorProfileId is { } authorId)
            details.Author = await GetAuthorAsync(authorId);

        return new Response<ArticleDetails> { Status = 200, Message = "Article récupéré avec succès", Data = details };
    }

    /// <summary>Profil public d'un prof auteur, null si le compte est archivé ou n'est plus prof.</summary>
    private async Task<ArticleAuthor?> GetAuthorAsync(Guid userId)
    {
        var author = await context
            .Users.AsNoTracking()
            .Where(u => u.Id == userId && u.ArchivedAt == null && u.Teacher != null)
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Title, u.Description, u.ImgUrl })
            .FirstOrDefaultAsync();

        return author is null
            ? null
            : new ArticleAuthor
            {
                Id = author.Id,
                FullName = $"{author.FirstName} {author.LastName}".Trim(),
                Title = string.IsNullOrWhiteSpace(author.Title) ? null : author.Title.Trim(),
                DescriptionHtml = sanitizer.Sanitize(author.Description),
                ImgUrl = storage.Url(author.ImgUrl),
            };
    }

    /// <summary>
    /// Articles publiés de même langue partageant au moins un tag (sans tenir compte de la casse),
    /// du plus grand nombre de tags communs au plus petit, puis du plus récent au plus ancien.
    /// </summary>
    public async Task<Response<List<ArticleSummary>>> GetRelatedAsync(Guid id, int count)
    {
        var take = Math.Clamp(count, 1, HardCode.ARTICLE_RELATED_MAX_COUNT);
        var related = await cache.GetOrCreateAsync<List<ArticleSummary>?>(
            CacheKeys.ArticleRelated(id, take),
            async ct =>
            {
                var source = await Published()
                    .Where(a => a.Id == id)
                    .Select(a => new { a.Language, a.Tags })
                    .FirstOrDefaultAsync(ct);
                if (source is null)
                    return null;

                var tags = new HashSet<string>(source.Tags, StringComparer.OrdinalIgnoreCase);
                if (tags.Count == 0)
                    return [];

                var candidates = await Published()
                    .Where(a => a.Id != id && a.Language == source.Language)
                    .Select(a => new { a.Id, a.Tags, a.PublishedAt })
                    .ToListAsync(ct);
                var ids = candidates
                    .Select(c => new { c.Id, c.PublishedAt, Shared = c.Tags.Count(tags.Contains) })
                    .Where(c => c.Shared > 0)
                    .OrderByDescending(c => c.Shared)
                    .ThenByDescending(c => c.PublishedAt)
                    .ThenBy(c => c.Id)
                    .Take(take)
                    .Select(c => c.Id)
                    .ToList();
                if (ids.Count == 0)
                    return [];

                var articles = await Published()
                    .Where(a => ids.Contains(a.Id))
                    .Select(ToRow)
                    .ToDictionaryAsync(x => x.Article.Id, ct);
                return ids
                    .Where(articles.ContainsKey)
                    .Select(i => Summary(articles[i]))
                    .ToList();
            },
            CacheKeys.Articles,
            [CacheKeys.ARTICLES_TAG]
        );

        return related is null
            ? NotFound<List<ArticleSummary>>()
            : new Response<List<ArticleSummary>>
            {
                Status = 200,
                Message = "Articles similaires récupérés avec succès",
                Data = related,
                Count = related.Count,
            };
    }

    /// <summary>Tags des articles publiés, du plus utilisé au moins utilisé.</summary>
    public async Task<Response<List<string>>> GetTagsAsync(string? language)
    {
        var lang = NormalizeLanguage(language);
        var tags = await cache.GetOrCreateAsync(
            CacheKeys.ArticleTags(lang),
            async ct =>
            {
                var lists = await Published()
                    .Where(a => lang == null || a.Language == lang)
                    .Select(a => a.Tags)
                    .ToListAsync(ct);
                return lists
                    .SelectMany(t => t)
                    .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(g => g.Count())
                    .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.Key)
                    .ToList();
            },
            CacheKeys.Articles,
            [CacheKeys.ARTICLES_TAG]
        );

        return new Response<List<string>> { Status = 200, Message = "Tags récupérés avec succès", Data = tags, Count = tags.Count };
    }

    public async Task<Response<List<ArticleSitemapEntry>>> GetSitemapAsync()
    {
        var entries = await cache.GetOrCreateAsync(
            CacheKeys.ARTICLE_SITEMAP,
            async ct =>
                await Published()
                    .OrderByDescending(a => a.PublishedAt)
                    .Select(a => new ArticleSitemapEntry
                    {
                        Slug = a.Slug,
                        LastModified = a.UpdatedAt ?? a.PublishedAt ?? a.CreatedAt,
                    })
                    .ToListAsync(ct),
            CacheKeys.Articles,
            [CacheKeys.ARTICLES_TAG]
        );

        return new Response<List<ArticleSitemapEntry>> { Status = 200, Message = "Sitemap des articles", Data = entries, Count = entries.Count };
    }

    // ── Administration ──────────────────────────────────────────────────────────────────

    /// <summary>Liste d'édition : tous les articles, ou ceux de <paramref name="authorId"/> seulement (espace éditeur).</summary>
    public async Task<Response<List<ArticleSummary>>> GetAdminListAsync(ArticleQuery query, Guid? authorId = null)
    {
        var rows = query.Rows is > 0 and <= 100 ? query.Rows : 20;
        var articles = context.Articles.AsNoTracking();
        if (authorId is not null)
            articles = articles.Where(a => a.AuthorId == authorId);

        var language = NormalizeLanguage(query.Language);
        if (language is not null)
            articles = articles.Where(a => a.Language == language);
        if (!string.IsNullOrWhiteSpace(query.Search))
            articles = articles.Where(a => EF.Functions.ILike(a.Title, $"%{EscapeLike(query.Search.Trim())}%"));
        if (!string.IsNullOrWhiteSpace(query.Tag))
            articles = articles.Where(a => a.Tags.Contains(query.Tag.Trim()));

        var total = await articles.CountAsync();
        var items = await articles
            .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
            .ThenBy(a => a.Id)
            .Skip(Math.Max(0, query.First))
            .Take(rows)
            .Select(ToRow)
            .ToListAsync();

        return new Response<List<ArticleSummary>>
        {
            Status = 200,
            Message = "Articles récupérés avec succès",
            Data = items.Select(Summary).ToList(),
            Count = total,
        };
    }

    /// <summary>Article quel que soit son statut : édition et aperçu d'un brouillon.</summary>
    public async Task<Response<ArticleDetails>> GetByIdAsync(Guid id, CallerIdentity? caller)
    {
        var found = await context
            .Articles.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(ToRow)
            .FirstOrDefaultAsync();

        return found is null || !await CanWriteAsync(found.Article, caller)
            ? NotFound<ArticleDetails>()
            : new Response<ArticleDetails>
            {
                Status = 200,
                Message = "Article récupéré avec succès",
                Data = Details(found),
            };
    }

    public async Task<Response<ArticleDetails>> CreateAsync(ArticleUpsert dto, CallerIdentity? caller)
    {
        if (caller is null)
            return Fail<ArticleDetails>(401, "Utilisateur non authentifié");
        if (!caller.IsAdmin && !await IsEditorAsync(caller.UserId))
            return Fail<ArticleDetails>(403, "Rôle éditeur requis");

        await ForceAuthorAsync(dto, caller);

        var article = new Article
        {
            Id = Guid.NewGuid(),
            AuthorId = caller.UserId,
            Status = ArticleStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        if (Apply(article, dto) is { } invalid)
            return Fail<ArticleDetails>(400, invalid);

        article.Slug = await UniqueSlugAsync(SlugOrFallback(dto.Slug, dto.Title), article.Id);

        context.Articles.Add(article);
        if (await SaveAsync() is { } conflict)
            return conflict.As<ArticleDetails>();

        return new Response<ArticleDetails>
        {
            Status = 201,
            Message = "Article créé avec succès",
            Data = ResolveImages(new ArticleDetails(article)),
        };
    }

    public async Task<Response<ArticleDetails>> UpdateAsync(Guid id, ArticleUpsert dto, CallerIdentity? caller)
    {
        var article = await context.Articles.FirstOrDefaultAsync(a => a.Id == id);
        if (article is null || !await CanWriteAsync(article, caller))
            return NotFound<ArticleDetails>();

        await ForceAuthorAsync(dto, caller!);

        var previousImages = ImageKeys(article);

        if (Apply(article, dto) is { } invalid)
            return Fail<ArticleDetails>(400, invalid);

        if (article.Status == ArticleStatus.Published && PublishBlocker(article) is { } blocker)
            return Fail<ArticleDetails>(400, blocker);

        // Le slug porte l'URL indexée : il ne bouge plus après la première publication.
        if (article.PublishedAt is null && !string.IsNullOrWhiteSpace(dto.Slug))
        {
            var wanted = SlugOrFallback(dto.Slug, dto.Title);
            if (wanted != article.Slug)
                article.Slug = await UniqueSlugAsync(wanted, article.Id);
        }

        article.UpdatedAt = DateTimeOffset.UtcNow;
        if (await SaveAsync() is { } conflict)
            return conflict.As<ArticleDetails>();

        await DeleteImagesAsync(previousImages.Except(ImageKeys(article)));

        return new Response<ArticleDetails>
        {
            Status = 200,
            Message = "Article mis à jour avec succès",
            Data = ResolveImages(new ArticleDetails(article)),
        };
    }

    public async Task<Response<ArticleDetails>> SetPublishedAsync(Guid id, bool published, CallerIdentity? caller)
    {
        var article = await context.Articles.FirstOrDefaultAsync(a => a.Id == id);
        if (article is null || !await CanWriteAsync(article, caller))
            return NotFound<ArticleDetails>();

        if (published)
        {
            if (PublishBlocker(article) is { } blocker)
                return Fail<ArticleDetails>(400, blocker);

            article.Status = ArticleStatus.Published;
            article.PublishedAt ??= DateTimeOffset.UtcNow;
        }
        else
        {
            // PublishedAt reste posé : il fige le slug et garde la date d'origine à la republication.
            article.Status = ArticleStatus.Draft;
        }

        article.UpdatedAt = DateTimeOffset.UtcNow;
        await SaveAsync();

        return new Response<ArticleDetails>
        {
            Status = 200,
            Message = published ? "Article publié" : "Article repassé en brouillon",
            Data = ResolveImages(new ArticleDetails(article)),
        };
    }

    public async Task<Response<object>> ArchiveAsync(Guid id, CallerIdentity? caller)
    {
        var article = await context.Articles.FirstOrDefaultAsync(a => a.Id == id);
        if (article is null || !await CanWriteAsync(article, caller))
            return NotFound<object>();

        article.ArchivedAt = DateTimeOffset.UtcNow;
        article.Status = ArticleStatus.Draft;
        await SaveAsync();

        return new Response<object> { Status = 200, Message = "Article archivé" };
    }

    // ── Droits d'écriture ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Rôle relu en base plutôt que dans le jeton : un retrait par l'admin coupe l'écriture tout
    /// de suite, sans attendre l'expiration du jeton d'accès.
    /// </summary>
    private Task<bool> IsEditorAsync(Guid userId) =>
        context.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == HardCode.ROLE_EDITOR);

    /// <summary>Un admin écrit sur tout article, un éditeur sur les siens seulement.</summary>
    private async Task<bool> CanWriteAsync(Article article, CallerIdentity? caller) =>
        caller is not null
        && (caller.IsAdmin || (article.AuthorId == caller.UserId && await IsEditorAsync(caller.UserId)));

    /// <summary>Un éditeur signe de son nom : le nom saisi n'est retenu que pour un admin.</summary>
    private async Task ForceAuthorAsync(ArticleUpsert dto, CallerIdentity caller)
    {
        if (caller.IsAdmin)
            return;

        var name = await context
            .Users.Where(u => u.Id == caller.UserId)
            .Select(u => u.FirstName + " " + u.LastName)
            .FirstAsync();
        dto.AuthorDisplayName = name.Trim();
    }

    // ── Règles d'écriture ───────────────────────────────────────────────────────────────

    /// <summary>Copie le DTO dans l'entité après nettoyage ; renvoie le motif de refus éventuel.</summary>
    private string? Apply(Article article, ArticleUpsert dto)
    {
        var language = NormalizeLanguage(dto.Language);
        if (language is null)
            return "Langue non prise en charge";

        var title = dto.Title?.Trim() ?? string.Empty;
        if (title.Length == 0)
            return "Le titre est requis";
        if (title.Length > HardCode.ARTICLE_TITLE_MAX_LENGTH)
            return $"Le titre ne peut dépasser {HardCode.ARTICLE_TITLE_MAX_LENGTH} caractères";

        var author = dto.AuthorDisplayName?.Trim() ?? string.Empty;
        if (author.Length == 0)
            return "Le nom de l'auteur est requis";

        if (!IsArticleImageKey(dto.CoverImageKey))
            return "Image de couverture invalide";

        var tags = (dto.Tags ?? [])
            .Select(t => t?.Trim() ?? string.Empty)
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (tags.Count > HardCode.ARTICLE_MAX_TAGS)
            return $"Un article porte au plus {HardCode.ARTICLE_MAX_TAGS} tags";
        if (tags.Any(t => t.Length > HardCode.ARTICLE_TAG_MAX_LENGTH))
            return $"Un tag ne peut dépasser {HardCode.ARTICLE_TAG_MAX_LENGTH} caractères";

        var inputs = dto.Sections ?? [];
        if (inputs.Count > HardCode.ARTICLE_MAX_SECTIONS)
            return $"Un article compte au plus {HardCode.ARTICLE_MAX_SECTIONS} sections";

        var anchors = new HashSet<string>(StringComparer.Ordinal);
        var sections = new List<ArticleSection>(inputs.Count);
        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            var sectionTitle = input.Title?.Trim() ?? string.Empty;
            if (sectionTitle.Length == 0)
                return $"La section {i + 1} n'a pas de titre";

            var html = sanitizer.Sanitize(input.Html);
            if (sanitizer.TextLength(html) > HardCode.ARTICLE_SECTION_TEXT_MAX_LENGTH)
                return $"Le texte de la section {i + 1} dépasse {HardCode.ARTICLE_SECTION_TEXT_MAX_LENGTH} caractères";

            var imageKey = string.IsNullOrWhiteSpace(input.ImageKey) ? null : input.ImageKey.Trim();
            if (!IsArticleImageKey(imageKey))
                return $"Image invalide dans la section {i + 1}";

            if (html is null && imageKey is null)
                return $"La section {i + 1} n'a ni texte ni image";

            var anchorBase = SlugGenerator.Slugify(sectionTitle, 60);
            var anchor = SlugGenerator.MakeUnique(anchorBase.Length > 0 ? anchorBase : $"section-{i + 1}", anchors.Contains);
            anchors.Add(anchor);

            sections.Add(new ArticleSection
            {
                Anchor = anchor,
                Title = sectionTitle,
                Html = html,
                ImageKey = imageKey,
                ImageAlt = Clean(input.ImageAlt),
                ImageCaption = Clean(input.ImageCaption),
            });
        }

        var refInputs = dto.References ?? [];
        if (refInputs.Count > HardCode.ARTICLE_MAX_REFERENCES)
            return $"Un article compte au plus {HardCode.ARTICLE_MAX_REFERENCES} références";

        var references = new List<ArticleReference>(refInputs.Count);
        foreach (var input in refInputs)
        {
            var label = input.Label?.Trim() ?? string.Empty;
            if (label.Length == 0)
                return "Chaque référence doit avoir un libellé";

            var url = Clean(input.Url);
            if (url is not null && !IsHttpUrl(url))
                return $"Lien de référence invalide : {url}";

            references.Add(new ArticleReference { Label = label, Url = url, Doi = Clean(input.Doi) });
        }

        var subtitle = Clean(dto.Subtitle);
        var intro = Clean(dto.Intro);

        article.Language = language;
        article.Title = title;
        article.Subtitle = subtitle;
        article.Intro = intro;
        article.AuthorDisplayName = author;
        article.Tags = tags;
        article.CoverImageKey = Clean(dto.CoverImageKey);
        article.CoverImageAlt = Clean(dto.CoverImageAlt);
        // Nouvelle instance : EF suit le document jsonb comme un tout.
        article.Content = new ArticleContent { Sections = sections, References = references };
        article.Excerpt = intro is not null
            ? Truncate(intro, HardCode.ARTICLE_EXCERPT_LENGTH)
            : subtitle is not null && subtitle.Length <= HardCode.ARTICLE_EXCERPT_LENGTH
                ? subtitle
                : sanitizer.Excerpt(sections.FirstOrDefault(s => s.Html is not null)?.Html, HardCode.ARTICLE_EXCERPT_LENGTH);

        return null;
    }

    private static string Truncate(string text, int maxLength)
    {
        var flat = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (flat.Length <= maxLength)
            return flat;
        var cut = flat[..maxLength];
        var lastSpace = cut.LastIndexOf(' ');
        return (lastSpace > maxLength / 2 ? cut[..lastSpace] : cut).TrimEnd() + "…";
    }

    private static string? PublishBlocker(Article article) =>
        article.Content.Sections.Count == 0 ? "Un article publié doit compter au moins une section" : null;

    private async Task<string> UniqueSlugAsync(string wanted, Guid selfId)
    {
        // Les articles archivés gardent leur slug : l'index unique les compte aussi.
        var taken = await context
            .Articles.IgnoreQueryFilters()
            .Where(a => a.Id != selfId && (a.Slug == wanted || a.Slug.StartsWith(wanted + "-")))
            .Select(a => a.Slug)
            .ToListAsync();
        var set = taken.ToHashSet(StringComparer.Ordinal);
        return SlugGenerator.MakeUnique(wanted, set.Contains);
    }

    private static string SlugOrFallback(string? slug, string title)
    {
        var fromSlug = SlugGenerator.Slugify(slug);
        if (fromSlug.Length > 0)
            return fromSlug;
        var fromTitle = SlugGenerator.Slugify(title);
        return fromTitle.Length > 0 ? fromTitle : "article";
    }

    private async Task<Response<object>?> SaveAsync()
    {
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            return new Response<object> { Status = 409, Message = "Ce slug est déjà utilisé par un autre article" };
        }

        await cache.RemoveByTagAsync(CacheKeys.ARTICLES_TAG);
        return null;
    }

    private async Task DeleteImagesAsync(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            // Une image peut encore servir à un autre article (copie d'un brouillon) : on vérifie.
            var stillUsed = await context
                .Articles.IgnoreQueryFilters()
                .AnyAsync(a => a.CoverImageKey == key || a.Content.Sections.Any(s => s.ImageKey == key));
            if (stillUsed)
                continue;
            await storage.Delete(key, CancellationToken.None);
            logger.LogInformation("Image d'article supprimée : {Key}", key);
        }
    }

    // ── Outils ──────────────────────────────────────────────────────────────────────────

    private sealed record ArticleRow(Article Article, int Comments, Guid? TeacherAuthorId);

    /// <summary>
    /// Projection commune des lectures. L'identifiant de l'auteur n'est exposé que s'il est prof :
    /// il sert de lien vers son profil public, jamais vers un compte d'administration.
    /// </summary>
    private static readonly Expression<Func<Article, ArticleRow>> ToRow = a =>
        new ArticleRow(a, a.Comments.Count, a.Author != null && a.Author.Teacher != null ? a.AuthorId : null);

    private ArticleSummary Summary(ArticleRow row) =>
        ResolveImages(new ArticleSummary(row.Article, row.Comments) { AuthorProfileId = row.TeacherAuthorId });

    private ArticleDetails Details(ArticleRow row) =>
        ResolveImages(new ArticleDetails(row.Article, row.Comments) { AuthorProfileId = row.TeacherAuthorId });

    private IQueryable<Article> Published() =>
        context.Articles.AsNoTracking().Where(a => a.Status == ArticleStatus.Published);

    private T ResolveImages<T>(T summary)
        where T : ArticleSummary
    {
        summary.CoverImageUrl = storage.Url(summary.CoverImageKey);
        if (summary is ArticleDetails details)
        {
            foreach (var section in details.Sections)
                section.ImageUrl = storage.Url(section.ImageKey);
        }
        return summary;
    }

    private static HashSet<string> ImageKeys(Article article)
    {
        var keys = article.Content.Sections.Select(s => s.ImageKey).Append(article.CoverImageKey);
        return keys.Where(k => k is not null).Select(k => k!).ToHashSet(StringComparer.Ordinal);
    }

    private static bool IsArticleImageKey(string? key) =>
        string.IsNullOrWhiteSpace(key)
        || (StorageService.IsValidKey(key.Trim()) && key.Trim().StartsWith(StorageService.ArticleFolder + "/", StringComparison.Ordinal));

    private static bool IsHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string? NormalizeLanguage(string? language)
    {
        var lang = language?.Trim().ToLowerInvariant();
        return lang is not null && HardCode.ARTICLE_LANGUAGES.Contains(lang) ? lang : null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static Response<T> NotFound<T>() => Fail<T>(404, "Article introuvable");

    private static Response<T> Fail<T>(int status, string message) => new() { Status = status, Message = message };
}

internal static class ResponseExtensions
{
    public static Response<T> As<T>(this Response<object> response) =>
        new() { Status = response.Status, Message = response.Message };
}
