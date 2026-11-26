namespace RestaurantHandler.Domain.Policies;

/// <summary>
/// Identifiants d'entreprise français, contrôlés sans appel externe (statut pro déclaratif) :
/// SIREN et SIRET par la clé de Luhn, n° de TVA intracommunautaire dérivé du SIREN.
/// </summary>
public static class BusinessIdentityPolicy
{
    /// <summary>SIREN de La Poste : ses SIRET suivent une clé « somme des chiffres multiple de 5 ».</summary>
    private const string LaPosteSiren = "356000000";

    /// <summary>Retire espaces, points et tirets ; met en majuscules.</summary>
    public static string Normalize(string? value) =>
        new string((value ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c is not ('.' or '-')).ToArray())
            .ToUpperInvariant();

    public static bool IsValidSiren(string? siren) =>
        siren is { Length: 9 } && siren.All(char.IsAsciiDigit) && Luhn(siren);

    public static bool IsValidSiret(string? siret, string siren)
    {
        if (siret is not { Length: 14 } || !siret.All(char.IsAsciiDigit) || !siret.StartsWith(siren))
            return false;
        return siren == LaPosteSiren ? siret.Sum(c => c - '0') % 5 == 0 : Luhn(siret);
    }

    /// <summary>« FR » + clé (12 + 3 × (SIREN mod 97)) mod 97 sur deux chiffres + SIREN.</summary>
    public static string FrenchVatNumber(string siren)
    {
        var key = (12 + 3 * (long.Parse(siren) % 97)) % 97;
        return $"FR{key:00}{siren}";
    }

    /// <summary>Message d'erreur destiné au client, ou null si les identifiants sont cohérents.</summary>
    public static string? Check(string siren, string? siret, string? vatNumber)
    {
        if (!IsValidSiren(siren))
            return "Le numéro SIREN est invalide (9 chiffres).";
        if (siret is not null && !IsValidSiret(siret, siren))
            return "Le numéro SIRET est invalide ou ne correspond pas au SIREN.";
        if (vatNumber is not null && vatNumber != FrenchVatNumber(siren))
            return "Le numéro de TVA intracommunautaire ne correspond pas au SIREN.";
        return null;
    }

    private static bool Luhn(string digits)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var d = digits[digits.Length - 1 - i] - '0';
            if (i % 2 == 1)
            {
                d *= 2;
                if (d > 9)
                    d -= 9;
            }
            sum += d;
        }
        return sum % 10 == 0;
    }
}
