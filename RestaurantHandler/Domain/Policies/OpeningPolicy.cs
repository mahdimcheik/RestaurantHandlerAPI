using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Domain.Policies;

public enum OpeningStatus
{
    Open,
    Closed,
    Paused,
}

/// <summary>
/// État d'ouverture à un instant. <c>Paused</c> n'est possible que pendant une plage ouverte :
/// un restaurant fermé reste « Fermé » même si la pause court encore.
/// </summary>
public sealed record OpeningState(
    OpeningStatus Status,
    DateTimeOffset? ClosesAt,
    DateTimeOffset? NextOpeningAt,
    DateTimeOffset? PausedUntil,
    string? ClosureReason
);

/// <summary>Intervalle semi-ouvert [Start, End[ en heure murale du restaurant.</summary>
public readonly record struct LocalInterval(DateTime Start, DateTime End);

/// <summary>Horaires, fermetures et pause → ouverture (US-E03-1 à 3, R7).</summary>
public static class OpeningPolicy
{
    /// <summary>Au-delà, on considère qu'il n'y a pas de réouverture connue.</summary>
    public const int LookAheadDays = 31;

    public static bool IsPauseActive(bool isPaused, DateTimeOffset? pausedUntil, DateTimeOffset nowUtc) =>
        isPaused && (pausedUntil is null || pausedUntil > nowUtc);

    /// <summary>Plages ouvertes d'un jour local, fermetures déduites, triées.</summary>
    public static IReadOnlyList<LocalInterval> OpenIntervals(
        DateOnly day,
        IEnumerable<OpeningHours> hours,
        IReadOnlyCollection<Closure> closures
    )
    {
        var date = day.ToDateTime(TimeOnly.MinValue);
        var intervals = hours
            .Where(h => h.ArchivedAt is null && h.DayOfWeek == day.DayOfWeek && h.End > h.Start)
            .OrderBy(h => h.Start)
            .Select(h => new LocalInterval(date + h.Start.ToTimeSpan(), date + h.End.ToTimeSpan()))
            .ToList();

        foreach (var closure in closures.Where(c => c.ArchivedAt is null))
            intervals = intervals.SelectMany(i => Subtract(i, closure)).ToList();

        return intervals;
    }

    public static OpeningState Compute(
        DateTimeOffset nowUtc,
        IReadOnlyCollection<OpeningHours> hours,
        IReadOnlyCollection<Closure> closures,
        bool isPaused,
        DateTimeOffset? pausedUntil
    )
    {
        var nowLocal = RestaurantClock.ToLocal(nowUtc);
        var today = DateOnly.FromDateTime(nowLocal);

        var current = OpenIntervals(today, hours, closures)
            .Cast<LocalInterval?>()
            .FirstOrDefault(i => i!.Value.Start <= nowLocal && nowLocal < i.Value.End);

        if (current is { } open)
        {
            var closesAt = RestaurantClock.ToUtc(open.End);
            return IsPauseActive(isPaused, pausedUntil, nowUtc)
                ? new OpeningState(OpeningStatus.Paused, closesAt, null, pausedUntil, null)
                : new OpeningState(OpeningStatus.Open, closesAt, null, null, null);
        }

        var reason = closures
            .Where(c => c.ArchivedAt is null && c.StartLocal <= nowLocal && nowLocal < c.EndLocal)
            .Select(c => c.Reason)
            .FirstOrDefault(r => !string.IsNullOrWhiteSpace(r));

        return new OpeningState(OpeningStatus.Closed, null, NextOpening(nowLocal, hours, closures), null, reason);
    }

    /// <summary>Premier début de plage strictement après <paramref name="nowLocal"/>.</summary>
    public static DateTimeOffset? NextOpening(
        DateTime nowLocal,
        IReadOnlyCollection<OpeningHours> hours,
        IReadOnlyCollection<Closure> closures
    )
    {
        var today = DateOnly.FromDateTime(nowLocal);
        for (var offset = 0; offset <= LookAheadDays; offset++)
        {
            var start = OpenIntervals(today.AddDays(offset), hours, closures)
                .Select(i => (DateTime?)i.Start)
                .FirstOrDefault(s => s > nowLocal);
            if (start is { } found)
                return RestaurantClock.ToUtc(found);
        }

        return null;
    }

    private static IEnumerable<LocalInterval> Subtract(LocalInterval interval, Closure closure)
    {
        if (closure.EndLocal <= interval.Start || closure.StartLocal >= interval.End)
        {
            yield return interval;
            yield break;
        }

        if (closure.StartLocal > interval.Start)
            yield return new LocalInterval(interval.Start, closure.StartLocal);
        if (closure.EndLocal < interval.End)
            yield return new LocalInterval(closure.EndLocal, interval.End);
    }
}
