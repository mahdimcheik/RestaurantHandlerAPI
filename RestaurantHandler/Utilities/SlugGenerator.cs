using System.Globalization;
using System.Text;

namespace RestaurantHandler.Utilities;

/// <summary>Identifiants lisibles pour les URL et les ancres : ASCII minuscule, chiffres et tirets.</summary>
public static class SlugGenerator
{
    public static string Slugify(string? text, int maxLength = 120)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var builder = new StringBuilder(text.Length);
        var pendingDash = false;
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            var lower = c switch
            {
                'œ' or 'Œ' => "oe",
                'æ' or 'Æ' => "ae",
                'ß' => "ss",
                _ => char.IsAsciiLetterOrDigit(c) ? char.ToLowerInvariant(c).ToString() : null,
            };

            if (lower is null)
            {
                pendingDash = builder.Length > 0;
                continue;
            }

            if (pendingDash)
                builder.Append('-');
            pendingDash = false;
            builder.Append(lower);
        }

        var slug = builder.ToString();
        return slug.Length <= maxLength ? slug : slug[..maxLength].TrimEnd('-');
    }

    /// <summary>Premier de <c>slug</c>, <c>slug-2</c>, <c>slug-3</c>… que <paramref name="taken"/> refuse.</summary>
    public static string MakeUnique(string slug, Func<string, bool> taken)
    {
        if (!taken(slug))
            return slug;

        for (var i = 2; ; i++)
        {
            var candidate = $"{slug}-{i}";
            if (!taken(candidate))
                return candidate;
        }
    }
}
