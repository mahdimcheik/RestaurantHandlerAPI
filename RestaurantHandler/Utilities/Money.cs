using System.Globalization;

namespace RestaurantHandler.Utilities;

/// <summary>Montants affichés aux utilisateurs : toujours au format français, quelle que soit la culture du serveur.</summary>
public static class Money
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public static string Format(int cents, string currency = "€") => string.Format(French, "{0:0.00} {1}", cents / 100m, currency);
}
