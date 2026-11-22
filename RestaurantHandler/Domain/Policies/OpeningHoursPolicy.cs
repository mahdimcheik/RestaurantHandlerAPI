namespace RestaurantHandler.Domain.Policies;

public readonly record struct WeeklyRange(DayOfWeek DayOfWeek, TimeOnly Start, TimeOnly End);

/// <summary>Validation d'une semaine d'horaires (US-E03-1).</summary>
public static class OpeningHoursPolicy
{
    /// <summary>Message d'erreur destiné au Manager, ou null si la semaine est valide.</summary>
    public static string? Validate(IReadOnlyCollection<WeeklyRange> ranges)
    {
        foreach (var r in ranges)
        {
            if (r.End <= r.Start)
                return $"{DayName(r.DayOfWeek)} : la plage {Format(r)} doit finir après son début "
                    + "(une plage ne passe pas minuit).";
        }

        foreach (var day in ranges.GroupBy(r => r.DayOfWeek))
        {
            var sorted = day.OrderBy(r => r.Start).ToList();
            for (var i = 1; i < sorted.Count; i++)
            {
                if (sorted[i].Start < sorted[i - 1].End)
                    return $"{DayName(day.Key)} : les plages {Format(sorted[i - 1])} et {Format(sorted[i])} se chevauchent.";
            }
        }

        return null;
    }

    public static string DayName(DayOfWeek day) =>
        day switch
        {
            DayOfWeek.Monday => "Lundi",
            DayOfWeek.Tuesday => "Mardi",
            DayOfWeek.Wednesday => "Mercredi",
            DayOfWeek.Thursday => "Jeudi",
            DayOfWeek.Friday => "Vendredi",
            DayOfWeek.Saturday => "Samedi",
            _ => "Dimanche",
        };

    private static string Format(WeeklyRange r) => $"{r.Start:HH\\:mm}–{r.End:HH\\:mm}";
}
