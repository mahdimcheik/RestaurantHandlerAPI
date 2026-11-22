using System.ComponentModel.DataAnnotations;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Models;

public class StatusReservationDetails
{
    /// <summary>
    /// Identifiant unique du statut
    /// </summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>
    /// Nom du statut
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Couleur associée au statut (code hexadécimal)
    /// </summary>
    [Required]
    public string Color { get; set; } = string.Empty;

    /// <summary>
    /// Icône associée au statut
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Code du statut, code unique pour l'identification
    /// </summary>
    [Required]
    public StatusReservationCode Code { get; set; }

    /// <summary>
    /// Traductions du nom par langue (clé = code langue minuscule, ex. "fr"/"en")
    /// </summary>
    [Required]
    public Dictionary<string, string> Translations { get; set; } = new();

    public StatusReservationDetails() { }

    public StatusReservationDetails(StatusReservation status)
    {
        Id = status.Id;
        Name = status.Name;
        Color = status.Color;
        Icon = status.Icon;
        Code = status.Code;
        Translations = status.Translations;
    }
}

public class StatusReservationCreate
{
    /// <summary>
    /// Nom du type de créneau
    /// </summary>
    /// <example>Cours individuel</example>
    [Required(ErrorMessage = "Le nom est requis")]
    [StringLength(64, ErrorMessage = "Le nom ne peut pas dépasser 64 caractères")]
    public required string Name { get; set; }

    /// <summary>
    /// Couleur associée au type de créneau (code hexadécimal)
    /// </summary>
    /// <example>#ff69b4</example>
    [Required(ErrorMessage = "La couleur est requise")]
    [StringLength(16, ErrorMessage = "La couleur ne peut pas dépasser 16 caractères")]
    [RegularExpression(
        @"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$",
        ErrorMessage = "La couleur doit être au format hexadécimal valide (ex: #ff69b4)"
    )]
    public required string Color { get; set; }

    /// <summary>
    /// Icône associée au type de créneau
    /// </summary>
    /// <example>fa-user</example>
    [StringLength(256, ErrorMessage = "L'icône ne peut pas dépasser 256 caractères")]
    public string? Icon { get; set; }

    [Required(ErrorMessage = "Les traductions sont requises")]
    [RequiresDefaultLanguage]
    public required Dictionary<string, string> Translations { get; set; }
}

/// <summary>
/// DTO pour la mise à jour d'un type de créneau existant
/// </summary>
public class StatusReservationUpdate
{
    /// <summary>
    /// Nom du type de créneau
    /// </summary>
    /// <example>Cours individuel</example>
    [Required(ErrorMessage = "Le nom est requis")]
    [StringLength(64, ErrorMessage = "Le nom ne peut pas dépasser 64 caractères")]
    public required string Name { get; set; }

    /// <summary>
    /// Couleur associée au type de créneau (code hexadécimal)
    /// </summary>
    /// <example>#ff69b4</example>
    [Required(ErrorMessage = "La couleur est requise")]
    [StringLength(16, ErrorMessage = "La couleur ne peut pas dépasser 16 caractères")]
    [RegularExpression(
        @"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$",
        ErrorMessage = "La couleur doit être au format hexadécimal valide (ex: #ff69b4)"
    )]
    public required string Color { get; set; }

    /// <summary>
    /// Icône associée au type de créneau
    /// </summary>
    /// <example>fa-user</example>
    [StringLength(256, ErrorMessage = "L'icône ne peut pas dépasser 256 caractères")]
    public string? Icon { get; set; }

    [Required(ErrorMessage = "Les traductions sont requises")]
    [RequiresDefaultLanguage]
    public required Dictionary<string, string> Translations { get; set; }

    public void UpdateModel(StatusReservation statusBooking)
    {
        statusBooking.Name = Name;
        statusBooking.Color = Color;
        statusBooking.Icon = Icon;
        statusBooking.Translations = Translations;
        statusBooking.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
