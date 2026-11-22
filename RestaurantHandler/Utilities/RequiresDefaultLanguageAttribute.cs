using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Utilities;

/// <summary>Valide un dictionnaire de traductions saisi par l'admin : la langue par défaut
/// est obligatoire, seules les langues de <see cref="HardCode.SUPPORTED_LANGUAGE_CODES"/>
/// sont acceptées, et aucune valeur ne peut être vide.</summary>
public class RequiresDefaultLanguageAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (
            value is not Dictionary<string, string> translations
            || !translations.TryGetValue(HardCode.DEFAULT_LANGUAGE_CODE, out var defaultValue)
            || string.IsNullOrWhiteSpace(defaultValue)
        )
        {
            return new ValidationResult(
                $"La traduction par défaut (\"{HardCode.DEFAULT_LANGUAGE_CODE}\") est requise."
            );
        }

        var unsupported = translations
            .Keys.Where(code => !HardCode.SUPPORTED_LANGUAGE_CODES.Contains(code))
            .ToList();
        if (unsupported.Count > 0)
        {
            return new ValidationResult(
                $"Langue(s) non supportée(s) : {string.Join(", ", unsupported)}. Langues acceptées : {string.Join(", ", HardCode.SUPPORTED_LANGUAGE_CODES)}."
            );
        }

        if (translations.Values.Any(string.IsNullOrWhiteSpace))
        {
            return new ValidationResult("Une traduction ne peut pas être vide.");
        }

        return ValidationResult.Success;
    }
}
