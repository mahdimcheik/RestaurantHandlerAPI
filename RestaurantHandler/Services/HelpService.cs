using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace RestaurantHandler.Services;

/// <summary>
/// Centre d'aide (ADR-0019) : écriture réservée aux admins, lecture publique des seules fiches
/// publiées. La recherche lit <see cref="HelpEntry.SearchText"/>, calculé à l'écriture.
/// </summary>
public partial class HelpService(MainContext context, RichTextSanitizer sanitizer, HybridCache cache)
{
    // ── Lecture publique ────────────────────────────────────────────────────────────────

    /// <summary>Toutes les fiches publiées d'une langue, par rubrique puis par ordre.</summary>
    public async Task<Response<List<HelpSummary>>> GetPublishedAsync(string? language)
    {
        var lang = NormalizeLanguage(language) ?? HardCode.ARTICLE_LANGUAGES[0];
        var items = await cache.GetOrCreateAsync(
            CacheKeys.HelpList(lang),
            async ct =>
                (
                    await Published()
                        .Where(h => h.Language == lang)
                        .OrderBy(h => h.SortOrder)
                        .ThenBy(h => h.Question)
                        .ToListAsync(ct)
                )
                    .Select(h => new HelpSummary(h))
                    .ToList(),
            CacheKeys.Articles,
            [CacheKeys.HELP_TAG]
        );

        return new Response<List<HelpSummary>> { Status = 200, Message = "Fiches d'aide récupérées avec succès", Data = items, Count = items.Count };
    }

    /// <summary>
    /// Fiches publiées contenant chacun des termes, sans tenir compte des accents ni de la casse.
    /// Celles dont la question contient le plus de termes passent devant.
    /// </summary>
    public async Task<Response<List<HelpSummary>>> SearchAsync(HelpSearchQuery query)
    {
        var terms = SearchTerms(query.Q);
        if (terms.Count == 0)
            return new Response<List<HelpSummary>> { Status = 200, Message = "Aucun terme recherché", Data = [], Count = 0 };

        var lang = NormalizeLanguage(query.Language) ?? HardCode.ARTICLE_LANGUAGES[0];
        var entries = Published().Where(h => h.Language == lang);
        if (query.Audience is { } audience && audience != HelpAudience.All)
            entries = entries.Where(h => h.Audience == HelpAudience.All || h.Audience == audience);
        foreach (var term in terms)
        {
            var pattern = $"%{EscapeLike(term)}%";
            entries = entries.Where(h => EF.Functions.Like(h.SearchText, pattern));
        }

        // Les fiches se comptent en dizaines : le classement se fait en mémoire.
        var found = await entries.Take(200).ToListAsync();
        var ranked = found
            .Select(h => new { Entry = h, Score = terms.Count(Normalize(h.Question).Contains) })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Entry.SortOrder)
            .ThenBy(x => x.Entry.Question, StringComparer.CurrentCultureIgnoreCase)
            .Take(HardCode.HELP_SEARCH_MAX_RESULTS)
            .Select(x => new HelpSummary(x.Entry))
            .ToList();

        return new Response<List<HelpSummary>> { Status = 200, Message = "Résultats de recherche", Data = ranked, Count = ranked.Count };
    }

    public async Task<Response<HelpDetails>> GetPublishedBySlugAsync(string slug)
    {
        var normalized = (slug ?? string.Empty).Trim().ToLowerInvariant();
        var details = await cache.GetOrCreateAsync(
            CacheKeys.HelpBySlug(normalized),
            async ct =>
            {
                var entry = await Published().FirstOrDefaultAsync(h => h.Slug == normalized, ct);
                if (entry is null)
                    return null;

                var related = await Published()
                    .Where(h => h.Id != entry.Id && h.Language == entry.Language && h.Category == entry.Category)
                    .OrderBy(h => h.SortOrder)
                    .ThenBy(h => h.Question)
                    .Take(HardCode.HELP_RELATED_COUNT)
                    .ToListAsync(ct);

                return new HelpDetails(entry) { Related = related.Select(h => new HelpSummary(h)).ToList() };
            },
            CacheKeys.Articles,
            [CacheKeys.HELP_TAG]
        );

        return details is null
            ? NotFound<HelpDetails>()
            : new Response<HelpDetails> { Status = 200, Message = "Fiche d'aide récupérée avec succès", Data = details };
    }

    public async Task<Response<List<HelpSitemapEntry>>> GetSitemapAsync()
    {
        var entries = await cache.GetOrCreateAsync(
            CacheKeys.HELP_SITEMAP,
            async ct =>
                await Published()
                    .OrderBy(h => h.Slug)
                    .Select(h => new HelpSitemapEntry { Slug = h.Slug, LastModified = h.UpdatedAt ?? h.PublishedAt ?? h.CreatedAt })
                    .ToListAsync(ct),
            CacheKeys.Articles,
            [CacheKeys.HELP_TAG]
        );

        return new Response<List<HelpSitemapEntry>> { Status = 200, Message = "Sitemap du centre d'aide", Data = entries, Count = entries.Count };
    }

    // ── Administration ──────────────────────────────────────────────────────────────────

    public async Task<Response<List<HelpSummary>>> GetAdminListAsync(HelpAdminQuery query)
    {
        var rows = query.Rows is > 0 and <= 100 ? query.Rows : 20;
        var entries = context.HelpEntries.AsNoTracking();

        var language = NormalizeLanguage(query.Language);
        if (language is not null)
            entries = entries.Where(h => h.Language == language);
        if (query.Category is { } category)
            entries = entries.Where(h => h.Category == category);
        if (!string.IsNullOrWhiteSpace(query.Search))
            entries = entries.Where(h => EF.Functions.ILike(h.Question, $"%{EscapeLike(query.Search.Trim())}%"));

        var total = await entries.CountAsync();
        var items = await entries
            .OrderBy(h => h.Category)
            .ThenBy(h => h.SortOrder)
            .ThenBy(h => h.Question)
            .Skip(Math.Max(0, query.First))
            .Take(rows)
            .ToListAsync();

        return new Response<List<HelpSummary>>
        {
            Status = 200,
            Message = "Fiches d'aide récupérées avec succès",
            Data = items.Select(h => new HelpSummary(h)).ToList(),
            Count = total,
        };
    }

    /// <summary>Fiche quel que soit son statut : édition d'un brouillon.</summary>
    public async Task<Response<HelpDetails>> GetByIdAsync(Guid id)
    {
        var entry = await context.HelpEntries.AsNoTracking().FirstOrDefaultAsync(h => h.Id == id);
        return entry is null
            ? NotFound<HelpDetails>()
            : new Response<HelpDetails> { Status = 200, Message = "Fiche d'aide récupérée avec succès", Data = new HelpDetails(entry) };
    }

    public async Task<Response<HelpDetails>> CreateAsync(HelpUpsert dto)
    {
        var entry = new HelpEntry
        {
            Id = Guid.NewGuid(),
            Status = HelpEntryStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        if (Apply(entry, dto) is { } invalid)
            return Fail<HelpDetails>(400, invalid);

        entry.Slug = await UniqueSlugAsync(SlugOrFallback(dto.Slug, entry.Question), entry.Id);

        context.HelpEntries.Add(entry);
        if (await SaveAsync() is { } conflict)
            return conflict;

        return new Response<HelpDetails> { Status = 201, Message = "Fiche d'aide créée avec succès", Data = new HelpDetails(entry) };
    }

    public async Task<Response<HelpDetails>> UpdateAsync(Guid id, HelpUpsert dto)
    {
        var entry = await context.HelpEntries.FirstOrDefaultAsync(h => h.Id == id);
        if (entry is null)
            return NotFound<HelpDetails>();

        if (Apply(entry, dto) is { } invalid)
            return Fail<HelpDetails>(400, invalid);

        if (entry.Status == HelpEntryStatus.Published && PublishBlocker(entry) is { } blocker)
            return Fail<HelpDetails>(400, blocker);

        // Le slug porte l'URL indexée : il ne bouge plus après la première publication.
        if (entry.PublishedAt is null && !string.IsNullOrWhiteSpace(dto.Slug))
        {
            var wanted = SlugOrFallback(dto.Slug, entry.Question);
            if (wanted != entry.Slug)
                entry.Slug = await UniqueSlugAsync(wanted, entry.Id);
        }

        entry.UpdatedAt = DateTimeOffset.UtcNow;
        if (await SaveAsync() is { } conflict)
            return conflict;

        return new Response<HelpDetails> { Status = 200, Message = "Fiche d'aide mise à jour avec succès", Data = new HelpDetails(entry) };
    }

    public async Task<Response<HelpDetails>> SetPublishedAsync(Guid id, bool published)
    {
        var entry = await context.HelpEntries.FirstOrDefaultAsync(h => h.Id == id);
        if (entry is null)
            return NotFound<HelpDetails>();

        if (published)
        {
            if (PublishBlocker(entry) is { } blocker)
                return Fail<HelpDetails>(400, blocker);

            entry.Status = HelpEntryStatus.Published;
            entry.PublishedAt ??= DateTimeOffset.UtcNow;
        }
        else
        {
            // PublishedAt reste posé : il fige le slug.
            entry.Status = HelpEntryStatus.Draft;
        }

        entry.UpdatedAt = DateTimeOffset.UtcNow;
        await SaveAsync();

        return new Response<HelpDetails>
        {
            Status = 200,
            Message = published ? "Fiche d'aide publiée" : "Fiche d'aide repassée en brouillon",
            Data = new HelpDetails(entry),
        };
    }

    public async Task<Response<object>> ArchiveAsync(Guid id)
    {
        var entry = await context.HelpEntries.FirstOrDefaultAsync(h => h.Id == id);
        if (entry is null)
            return NotFound<object>();

        entry.ArchivedAt = DateTimeOffset.UtcNow;
        entry.Status = HelpEntryStatus.Draft;
        await SaveAsync();

        return new Response<object> { Status = 200, Message = "Fiche d'aide archivée" };
    }

    // ── Règles d'écriture ───────────────────────────────────────────────────────────────

    /// <summary>Copie le DTO dans l'entité après nettoyage ; renvoie le motif de refus éventuel.</summary>
    private string? Apply(HelpEntry entry, HelpUpsert dto)
    {
        var language = NormalizeLanguage(dto.Language);
        if (language is null)
            return "Langue non prise en charge";

        var question = dto.Question?.Trim() ?? string.Empty;
        if (question.Length == 0)
            return "La question est requise";
        if (question.Length > HardCode.HELP_QUESTION_MAX_LENGTH)
            return $"La question ne peut dépasser {HardCode.HELP_QUESTION_MAX_LENGTH} caractères";

        var summary = Clean(dto.Summary);
        if (summary is not null && summary.Length > HardCode.HELP_SUMMARY_MAX_LENGTH)
            return $"Le résumé ne peut dépasser {HardCode.HELP_SUMMARY_MAX_LENGTH} caractères";

        var answer = sanitizer.Sanitize(dto.AnswerHtml);
        if (sanitizer.TextLength(answer) > HardCode.HELP_ANSWER_TEXT_MAX_LENGTH)
            return $"La réponse dépasse {HardCode.HELP_ANSWER_TEXT_MAX_LENGTH} caractères";

        if (!Enum.IsDefined(dto.Category))
            return "Rubrique inconnue";
        if (!Enum.IsDefined(dto.Audience))
            return "Public inconnu";

        var keywords = (dto.Keywords ?? [])
            .Select(k => k?.Trim() ?? string.Empty)
            .Where(k => k.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (keywords.Count > HardCode.HELP_MAX_KEYWORDS)
            return $"Une fiche porte au plus {HardCode.HELP_MAX_KEYWORDS} mots-clés";
        if (keywords.Any(k => k.Length > HardCode.HELP_KEYWORD_MAX_LENGTH))
            return $"Un mot-clé ne peut dépasser {HardCode.HELP_KEYWORD_MAX_LENGTH} caractères";

        string? videoId = null;
        if (Clean(dto.YoutubeUrl) is { } url)
        {
            videoId = ParseYoutubeVideoId(url);
            if (videoId is null)
                return "Lien YouTube non reconnu";
        }

        entry.Language = language;
        entry.Question = question;
        entry.Summary = summary;
        entry.AnswerHtml = answer;
        entry.Category = dto.Category;
        entry.Audience = dto.Audience;
        entry.Keywords = keywords;
        entry.YoutubeVideoId = videoId;
        entry.SortOrder = Math.Max(0, dto.SortOrder);
        entry.SearchText = Normalize(
            string.Join(' ', new[] { question, summary, string.Join(' ', keywords), sanitizer.Excerpt(answer, int.MaxValue) }.Where(s => s is not null))
        );

        return null;
    }

    private static string? PublishBlocker(HelpEntry entry) =>
        entry.AnswerHtml is null && entry.YoutubeVideoId is null ? "Une fiche publiée doit avoir une réponse ou une vidéo" : null;

    /// <summary>
    /// Identifiant de vidéo d'un lien YouTube (youtu.be/ID, youtube.com/watch?v=ID, /embed/ID,
    /// /shorts/ID, /live/ID, youtube-nocookie.com) ou d'un identifiant seul ; null sinon.
    /// </summary>
    public static string? ParseYoutubeVideoId(string input)
    {
        var value = input.Trim();
        if (VideoIdRegex().IsMatch(value))
            return value;

        if (!Uri.TryCreate(value.Contains("://") ? value : "https://" + value, UriKind.Absolute, out var uri))
            return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return null;

        var host = uri.Host.ToLowerInvariant();
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string? candidate = null;

        if (host is "youtu.be" or "www.youtu.be")
            candidate = segments.FirstOrDefault();
        else if (host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or "music.youtube.com" or "youtube-nocookie.com" or "www.youtube-nocookie.com")
        {
            if (segments.Length == 1 && segments[0] == "watch")
                candidate = QueryValue(uri.Query, "v");
            else if (segments.Length >= 2 && segments[0] is "embed" or "shorts" or "live" or "v")
                candidate = segments[1];
        }

        return candidate is not null && VideoIdRegex().IsMatch(candidate) ? candidate : null;
    }

    private static string? QueryValue(string query, string name)
    {
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 && pair[0] == name)
                return Uri.UnescapeDataString(pair[1]);
        }
        return null;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex VideoIdRegex();

    /// <summary>Minuscules, sans accents ni ponctuation, espaces simples : forme commune de l'index et des termes.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            var mapped = c switch
            {
                'œ' or 'Œ' => "oe",
                'æ' or 'Æ' => "ae",
                'ß' => "ss",
                _ => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c).ToString() : null,
            };

            if (mapped is null)
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
                builder.Append(' ');
            pendingSpace = false;
            builder.Append(mapped);
        }

        return builder.ToString();
    }

    private static List<string> SearchTerms(string? query) =>
        Normalize(query)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2)
            .Distinct()
            .Take(HardCode.HELP_SEARCH_MAX_TERMS)
            .ToList();

    private async Task<string> UniqueSlugAsync(string wanted, Guid selfId)
    {
        // Les fiches archivées gardent leur slug : l'index unique les compte aussi.
        var taken = await context
            .HelpEntries.IgnoreQueryFilters()
            .Where(h => h.Id != selfId && (h.Slug == wanted || h.Slug.StartsWith(wanted + "-")))
            .Select(h => h.Slug)
            .ToListAsync();
        var set = taken.ToHashSet(StringComparer.Ordinal);
        return SlugGenerator.MakeUnique(wanted, set.Contains);
    }

    private static string SlugOrFallback(string? slug, string question)
    {
        var fromSlug = SlugGenerator.Slugify(slug);
        if (fromSlug.Length > 0)
            return fromSlug;
        var fromQuestion = SlugGenerator.Slugify(question);
        return fromQuestion.Length > 0 ? fromQuestion : "aide";
    }

    private async Task<Response<HelpDetails>?> SaveAsync()
    {
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            return Fail<HelpDetails>(409, "Ce slug est déjà utilisé par une autre fiche");
        }

        await cache.RemoveByTagAsync(CacheKeys.HELP_TAG);
        return null;
    }

    private IQueryable<HelpEntry> Published() =>
        context.HelpEntries.AsNoTracking().Where(h => h.Status == HelpEntryStatus.Published);

    private static string? NormalizeLanguage(string? language)
    {
        var lang = language?.Trim().ToLowerInvariant();
        return lang is not null && HardCode.ARTICLE_LANGUAGES.Contains(lang) ? lang : null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static Response<T> NotFound<T>() => Fail<T>(404, "Fiche d'aide introuvable");

    private static Response<T> Fail<T>(int status, string message) => new() { Status = status, Message = message };
}
