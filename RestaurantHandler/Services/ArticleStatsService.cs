using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

/// <summary>
/// Statistiques d'affichage des articles : un compteur par article et par jour UTC, incrémenté
/// par le navigateur à chaque chargement de la page publique. Aucun dédoublonnage ni filtrage
/// des robots au-delà de l'exécution du JavaScript : les chiffres sont des tendances.
/// </summary>
public class ArticleStatsService(MainContext context)
{
    public const int DEFAULT_DAYS = 30;
    public const int MAX_DAYS = 365;

    public async Task<Response<object>> RecordViewAsync(Guid articleId)
    {
        var today = Today();
        var published = nameof(ArticleStatus.Published);

        var inserted = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "ArticleViewDays" ("ArticleId", "Day", "Views")
            SELECT "Id", {today}, 1 FROM "Articles"
            WHERE "Id" = {articleId} AND "Status" = {published} AND "ArchivedAt" IS NULL
            ON CONFLICT ("ArticleId", "Day") DO UPDATE SET "Views" = "ArticleViewDays"."Views" + 1
            """
        );

        return inserted == 0
            ? new Response<object> { Status = 404, Message = "Article introuvable" }
            : new Response<object> { Status = 200, Message = "Affichage enregistré" };
    }

    public async Task<Response<ArticleStatsOverview>> GetOverviewAsync(int? days)
    {
        var (from, to, previousFrom) = Period(days);

        var rows = await context
            .Articles.AsNoTracking()
            .Select(a => new ArticleStatsRow
            {
                Id = a.Id,
                Slug = a.Slug,
                Title = a.Title,
                Language = a.Language,
                Status = a.Status,
                PublishedAt = a.PublishedAt,
                CommentsCount = a.Comments.Count,
                TotalViews = context.ArticleViewDays.Where(v => v.ArticleId == a.Id).Sum(v => (int?)v.Views) ?? 0,
                PeriodViews =
                    context.ArticleViewDays.Where(v => v.ArticleId == a.Id && v.Day >= from).Sum(v => (int?)v.Views) ?? 0,
            })
            .Where(r => r.Status == ArticleStatus.Published || r.TotalViews > 0)
            .OrderByDescending(r => r.PeriodViews)
            .ThenByDescending(r => r.TotalViews)
            .ThenBy(r => r.Title)
            .ToListAsync();

        var points = await context
            .ArticleViewDays.AsNoTracking()
            .Where(v => v.Day >= previousFrom)
            .Select(v => new { v.ArticleId, v.Day, v.Views })
            .ToListAsync();

        var current = points.Where(p => p.Day >= from).ToList();
        var byArticle = current.ToLookup(p => p.ArticleId);
        foreach (var row in rows)
            row.Series = Fill(from, to, byArticle[row.Id].Select(p => (p.Day, p.Views))).Select(p => p.Views).ToList();

        var overview = new ArticleStatsOverview
        {
            From = from,
            To = to,
            TotalViews = rows.Sum(r => r.TotalViews),
            PeriodViews = current.Sum(p => p.Views),
            PreviousPeriodViews = points.Where(p => p.Day < from).Sum(p => p.Views),
            Daily = Fill(from, to, current.Select(p => (p.Day, p.Views))),
            Articles = rows,
        };

        return new Response<ArticleStatsOverview>
        {
            Status = 200,
            Message = "Statistiques des articles",
            Data = overview,
            Count = rows.Count,
        };
    }

    public async Task<Response<ArticleStatsDetails>> GetArticleAsync(Guid articleId, int? days)
    {
        var (from, to, previousFrom) = Period(days);

        var article = await context
            .Articles.AsNoTracking()
            .Where(a => a.Id == articleId)
            .Select(a => new ArticleStatsRow
            {
                Id = a.Id,
                Slug = a.Slug,
                Title = a.Title,
                Language = a.Language,
                Status = a.Status,
                PublishedAt = a.PublishedAt,
                CommentsCount = a.Comments.Count,
                TotalViews = context.ArticleViewDays.Where(v => v.ArticleId == a.Id).Sum(v => (int?)v.Views) ?? 0,
            })
            .FirstOrDefaultAsync();

        if (article is null)
            return new Response<ArticleStatsDetails> { Status = 404, Message = "Article introuvable" };

        var points = await context
            .ArticleViewDays.AsNoTracking()
            .Where(v => v.ArticleId == articleId && v.Day >= previousFrom)
            .Select(v => new { v.Day, v.Views })
            .ToListAsync();

        var daily = Fill(from, to, points.Where(p => p.Day >= from).Select(p => (p.Day, p.Views)));
        article.PeriodViews = daily.Sum(p => p.Views);
        article.Series = daily.Select(p => p.Views).ToList();

        return new Response<ArticleStatsDetails>
        {
            Status = 200,
            Message = "Statistiques de l'article",
            Data = new ArticleStatsDetails
            {
                Article = article,
                From = from,
                To = to,
                TotalViews = article.TotalViews,
                PeriodViews = article.PeriodViews,
                PreviousPeriodViews = points.Where(p => p.Day < from).Sum(p => p.Views),
                Daily = daily,
            },
        };
    }

    private DateOnly Today() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

    private (DateOnly From, DateOnly To, DateOnly PreviousFrom) Period(int? days)
    {
        var length = Math.Clamp(days ?? DEFAULT_DAYS, 1, MAX_DAYS);
        var to = Today();
        var from = to.AddDays(1 - length);
        return (from, to, from.AddDays(-length));
    }

    private static List<ArticleViewPoint> Fill(DateOnly from, DateOnly to, IEnumerable<(DateOnly Day, int Views)> points)
    {
        var views = points.GroupBy(p => p.Day).ToDictionary(g => g.Key, g => g.Sum(p => p.Views));
        var result = new List<ArticleViewPoint>(to.DayNumber - from.DayNumber + 1);
        for (var day = from; day <= to; day = day.AddDays(1))
            result.Add(new ArticleViewPoint { Day = day, Views = views.GetValueOrDefault(day) });
        return result;
    }
}
