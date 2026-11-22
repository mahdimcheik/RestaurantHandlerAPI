using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Utilities;

/// <summary>
/// Valide un numéro de SIRET français (14 chiffres) via l'algorithme de Luhn,
/// avec l'exception La Poste (préfixe <c>356000000</c> : somme des chiffres
/// divisible par 5). Équivalent de <c>siretValidator()</c> côté Angular.
///
/// Un champ vide est considéré valide (laisser <see cref="RequiredAttribute"/>
/// gérer l'obligation). Accepte un <c>string</c> ou un type numérique.
/// </summary>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter,
    AllowMultiple = false
)]
public sealed class SiretAttribute : ValidationAttribute
{
    public SiretAttribute()
        : base("Numéro de SIRET invalide") { }

    public override bool IsValid(object? value)
    {
        // Champ vide = valide (laisser [Required] gérer ça)
        if (value is null)
        {
            return true;
        }

        var siret = value.ToString()?.Replace(" ", "") ?? "";

        if (siret.Length == 0)
        {
            return true;
        }

        // Le SIRET doit contenir exactement 14 chiffres
        if (siret.Length != 14 || !siret.All(char.IsDigit))
        {
            return false;
        }

        // Exception La Poste : valide si la somme des chiffres est divisible par 5
        if (siret.StartsWith("356000000", StringComparison.Ordinal))
        {
            var digitSum = siret.Sum(c => c - '0');
            return digitSum % 5 == 0;
        }

        // Algorithme de Luhn : on double un chiffre sur deux en partant de la
        // gauche (positions paires), retranche 9 si > 9, puis somme mod 10.
        var sum = 0;
        for (var i = 0; i < siret.Length; i++)
        {
            var digit = siret[i] - '0';
            if (i % 2 == 0)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }
            sum += digit;
        }

        return sum % 10 == 0;
    }
}
