using System.ComponentModel.DataAnnotations;
using RestaurantHandler.Models.Interfaces;

namespace RestaurantHandler.Models;

/// <summary>
/// Plage d'ouverture hebdomadaire, en heure locale du restaurant (Europe/Paris). Une plage ne
/// passe pas minuit : 18h30–00h30 se saisit en deux plages sur deux jours.
/// </summary>
public class OpeningHours : BaseModel
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
}

/// <summary>
/// Fermeture exceptionnelle (jour férié, congés) ou partielle (fermeture anticipée), bornes en
/// heure locale du restaurant, intervalle semi-ouvert [début, fin[.
/// </summary>
public class Closure : BaseModel
{
    public DateTime StartLocal { get; set; }
    public DateTime EndLocal { get; set; }

    /// <summary>Affiché au client.</summary>
    [MaxLength(256)]
    public string? Reason { get; set; }
}

/// <summary>Paramètres du restaurant : une seule ligne, d'identifiant <see cref="SingletonId"/>.</summary>
public class RestaurantSettings
{
    public static readonly Guid SingletonId = Guid.Parse("5e771465-0000-4000-8000-000000000001");

    [Key]
    public Guid Id { get; set; } = SingletonId;

    // Identité (PWA, reçus)
    [MaxLength(128)]
    public string TradeName { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? LegalName { get; set; }

    [MaxLength(256)]
    public string? Address { get; set; }

    [MaxLength(32)]
    public string? Phone { get; set; }

    [MaxLength(14)]
    public string? Siret { get; set; }

    [MaxLength(16)]
    public string? VatNumber { get; set; }

    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    // Service
    public int AsapDelayMinutes { get; set; } = 15;
    public int SlotDurationMinutes { get; set; } = 15;

    /// <summary>Nombre maximal de commandes par créneau.</summary>
    public int SlotCapacityOrders { get; set; } = 5;

    /// <summary>Délai minimal entre maintenant et le début du premier créneau proposé.</summary>
    public int MinLeadMinutes { get; set; } = 20;

    public int ApprovalTimeoutMinutes { get; set; } = 10;
    public int PickupGraceMinutes { get; set; } = 30;
    public int NoShowThreshold { get; set; } = 2;
    public int OnlinePaymentExpiryMinutes { get; set; } = 15;

    // Pause des commandes en ligne
    public bool IsPaused { get; set; }

    /// <summary>Reprise automatique ; null avec <see cref="IsPaused"/> = pause sans fin.</summary>
    public DateTimeOffset? PausedUntil { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>Historique des modifications de <see cref="RestaurantSettings"/> (US-E10-5).</summary>
public class RestaurantSettingsHistory
{
    [Key]
    public Guid Id { get; set; }

    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ChangedByUserId { get; set; }

    /// <summary>Paramètres après modification, en JSON.</summary>
    public string SnapshotJson { get; set; } = "{}";
}
