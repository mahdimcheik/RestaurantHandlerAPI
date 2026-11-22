using RestaurantHandler.Models.Interfaces;
using RestaurantHandler.Utilities;
using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models.Files
{
    public class PrivacyDocumentTypeDetails
    {
        [Required]
        public Guid Id { get; set; }
        [Required]
        public string Name { get; set; }
        public required string DisplayName { get; set; }
        public bool IsRequiredByDefault { get; set; }
        public string? Color { get; set; }
        public string? Icon { get; set; }
        public required Dictionary<string, string> Translations { get; set; }
    }
    /// <summary>Corps de création/mise à jour du catalogue de types (admin, Feature 10). Un
    /// changement d'<see cref="IsRequiredByDefault"/> n'affecte <see cref="Teacher.IsVerified"/>
    /// des profs existants qu'à leur prochaine mutation (upload/revue/demande) — pas de recalcul
    /// en masse, cohérent avec le flag passif décidé pour cette feature.</summary>
    public class PrivacyDocumentTypeCreate
    {
        [Required]
        [MaxLength(64)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [MaxLength(16)]
        public string Color { get; set; } = string.Empty;

        [MaxLength(256)]
        public string? Icon { get; set; }

        public bool IsRequiredByDefault { get; set; }

        [Required]
        [RequiresDefaultLanguage]
        public Dictionary<string, string> Translations { get; set; } = new();
    }

    public class PrivacyDocumentTypeUpdate : PrivacyDocumentTypeCreate { }
}
