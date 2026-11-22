namespace RestaurantHandler.Models;

public class ArticleViewPoint
{
    public DateOnly Day { get; set; }
    public int Views { get; set; }
}

public class ArticleStatsRow
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Language { get; set; } = "fr";
    public ArticleStatus Status { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public int CommentsCount { get; set; }
    public int TotalViews { get; set; }
    public int PeriodViews { get; set; }

    /// <summary>Affichages par jour, un entier par jour de la période (de <c>From</c> à <c>To</c>).</summary>
    public List<int> Series { get; set; } = [];
}

/// <summary>Bornes incluses, en jours UTC. La période précédente a la même durée et finit la veille de <see cref="From"/>.</summary>
public abstract class ArticleStatsPeriod
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int TotalViews { get; set; }
    public int PeriodViews { get; set; }
    public int PreviousPeriodViews { get; set; }
    public List<ArticleViewPoint> Daily { get; set; } = [];
}

public class ArticleStatsOverview : ArticleStatsPeriod
{
    public List<ArticleStatsRow> Articles { get; set; } = [];
}

public class ArticleStatsDetails : ArticleStatsPeriod
{
    public ArticleStatsRow Article { get; set; } = new();
}
