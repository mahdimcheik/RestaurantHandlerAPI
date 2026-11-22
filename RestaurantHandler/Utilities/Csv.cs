using System.Globalization;
using System.Text;

namespace RestaurantHandler.Utilities;

/// <summary>
/// Écriture CSV minimale (aucune dépendance CSV dans le projet). Format calibré pour
/// Excel FR : séparateur <c>;</c>, virgule décimale, BOM UTF-8 (sans lui Excel casse
/// les accents).
/// </summary>
public static class Csv
{
    private const char Separator = ';';
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public static byte[] Build(
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string?>> rows
    )
    {
        var sb = new StringBuilder();
        AppendRow(sb, headers);
        foreach (var row in rows)
            AppendRow(sb, row);

        // Encoding.UTF8 émet le BOM ; GetBytes ne le fait pas, on le préfixe à la main.
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    public static string Money(decimal? value) =>
        value?.ToString("0.00", Fr) ?? string.Empty;

    public static string Date(DateTimeOffset? value) =>
        value is null
            ? string.Empty
            : MonthRange.ToPlatformTime(value.Value).ToString("dd/MM/yyyy HH:mm", Fr);

    public static string Bool(bool value) => value ? "oui" : "non";

    private static void AppendRow(StringBuilder sb, IReadOnlyList<string?> cells)
    {
        for (var i = 0; i < cells.Count; i++)
        {
            if (i > 0)
                sb.Append(Separator);
            sb.Append(Escape(cells[i]));
        }
        sb.Append("\r\n");
    }

    // RFC 4180 : on n'entoure de guillemets que si nécessaire, en doublant ceux du contenu.
    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (
            value.IndexOf(Separator) < 0
            && value.IndexOf('"') < 0
            && value.IndexOf('\n') < 0
            && value.IndexOf('\r') < 0
        )
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
