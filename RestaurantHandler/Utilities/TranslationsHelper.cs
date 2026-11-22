namespace RestaurantHandler.Utilities;

public static class TranslationsHelper
{
    /// <summary>Copie nettoyée des traductions validées par <see cref="RequiresDefaultLanguageAttribute"/>
    /// (valeurs sans espaces superflus), prête à être stockée.</summary>
    public static Dictionary<string, string> Normalize(Dictionary<string, string> translations) =>
        translations.ToDictionary(t => t.Key, t => t.Value.Trim());

    /// <summary>Libellé dans la langue par défaut — sert de <c>Name</c>/<c>DisplayName</c> de repli.</summary>
    public static string DefaultLabel(Dictionary<string, string> translations) =>
        translations[HardCode.DEFAULT_LANGUAGE_CODE].Trim();
}
