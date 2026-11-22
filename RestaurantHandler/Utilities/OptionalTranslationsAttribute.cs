using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Utilities;

/// <summary>Valide un dictionnaire de traductions facultatif : absent ou vide est accepté ;
/// sinon seules les langues de <see cref="HardCode.SUPPORTED_LANGUAGE_CODES"/> sont acceptées,
/// et chaque valeur est non vide et ne dépasse pas <paramref name="maxLength"/> caractères.</summary>
public class OptionalTranslationsAttribute(int maxLength) : ValidationAttribute
{
    public int MaxLength { get; } = maxLength;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;

        if (value is not Dictionary<string, string> translations)
            return new ValidationResult("Le format des traductions est invalide.");

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

        if (translations.Values.Any(v => v.Trim().Length > MaxLength))
        {
            return new ValidationResult($"Une traduction ne peut pas dépasser {MaxLength} caractères.");
        }

        return ValidationResult.Success;
    }
}
