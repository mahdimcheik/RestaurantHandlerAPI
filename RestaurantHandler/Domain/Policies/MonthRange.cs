namespace RestaurantHandler.Utilities;

/// <summary>
/// Bornes d'un mois calendaire. Toutes les dates sont relues en UTC (converter
/// global, cf. <c>CustomDateTimeConversion</c>) : « juillet 2026 » côté plateforme
/// n'est donc PAS <c>2026-07-01T00:00Z</c> mais <c>2026-06-30T22:00Z</c>. On calcule
/// les bornes sur le calendrier local de la plateforme puis on les convertit en UTC.
/// </summary>
public static class MonthRange
{
    public static TimeZoneInfo PlatformTimeZone => RestaurantClock.TimeZone;

    public static bool IsValid(int year, int month) =>
        month >= 1 && month <= 12 && year >= 2000 && year <= 2100;

    /// <summary>
    /// Intervalle semi-ouvert <c>[From, To[</c> en UTC : une commande à cheval sur
    /// deux mois ne peut jamais être comptée deux fois.
    /// </summary>
    public static (DateTimeOffset From, DateTimeOffset To) ForMonth(int year, int month)
    {
        var localFrom = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var localTo = localFrom.AddMonths(1);

        return (
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localFrom, PlatformTimeZone)),
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localTo, PlatformTimeZone))
        );
    }

    /// <summary>Repasse une date UTC en heure locale plateforme (affichage / CSV).</summary>
    public static DateTimeOffset ToPlatformTime(DateTimeOffset utc) =>
        TimeZoneInfo.ConvertTime(utc, PlatformTimeZone);
}
