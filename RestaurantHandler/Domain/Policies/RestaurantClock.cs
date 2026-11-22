namespace RestaurantHandler.Utilities;

/// <summary>
/// Conversions entre l'heure UTC (stockage, API) et l'heure murale du restaurant (horaires,
/// fermetures, créneaux), changements d'heure compris.
/// </summary>
public static class RestaurantClock
{
    public static readonly TimeZoneInfo TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

    public static DateTime ToLocal(DateTimeOffset utc) =>
        DateTime.SpecifyKind(TimeZoneInfo.ConvertTime(utc, TimeZone).DateTime, DateTimeKind.Unspecified);

    /// <summary>
    /// Heure murale → instant UTC. Une heure qui n'existe pas (passage à l'heure d'été) est
    /// décalée d'une heure ; une heure ambiguë (passage à l'heure d'hiver) prend l'heure d'hiver.
    /// </summary>
    public static DateTimeOffset ToUtc(DateTime local)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (TimeZone.IsInvalidTime(local))
            local = local.AddHours(1);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, TimeZone), TimeSpan.Zero);
    }
}
