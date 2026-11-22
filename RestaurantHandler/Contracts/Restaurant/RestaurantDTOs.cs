using System.ComponentModel.DataAnnotations;
using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Models;

public class OpeningStateDto
{
    [Required] public OpeningStatus Status { get; set; } = OpeningStatus.Closed;
    public DateTimeOffset? ClosesAt { get; set; }
    public DateTimeOffset? NextOpeningAt { get; set; }
    public DateTimeOffset? PausedUntil { get; set; }
    public string? ClosureReason { get; set; }

    /// <summary>Délai ASAP annoncé (US-E03-5).</summary>
    [Required] public int AsapDelayMinutes { get; set; }
}

public class OpeningRangeDto
{
    [Required] public DayOfWeek DayOfWeek { get; set; }

    /// <summary>Heure locale du restaurant, ex. "11:30:00".</summary>
    [Required] public TimeOnly Start { get; set; }

    [Required] public TimeOnly End { get; set; }
}

public class WeekHoursUpdate
{
    /// <summary>La semaine complète : remplace toutes les plages existantes.</summary>
    [Required]
    public List<OpeningRangeDto> Ranges { get; set; } = [];
}

public class ClosureWrite : IValidatableObject
{
    /// <summary>Heure locale du restaurant, sans fuseau (ex. "2026-12-25T00:00:00").</summary>
    [Required] public DateTime StartLocal { get; set; }

    [Required] public DateTime EndLocal { get; set; }

    [MaxLength(256)]
    public string? Reason { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndLocal <= StartLocal)
            yield return new("La fin de la fermeture doit suivre son début.", [nameof(EndLocal)]);
    }
}

public class ClosureDetails
{
    [Required] public Guid Id { get; set; }
    [Required] public DateTime StartLocal { get; set; }
    [Required] public DateTime EndLocal { get; set; }
    public string? Reason { get; set; }
}

public class PauseRequest
{
    /// <summary>15, 30 ou 60 ; null = jusqu'à reprise manuelle.</summary>
    [AllowedValues(null, 15, 30, 60, ErrorMessage = "Durée de pause : 15, 30 ou 60 minutes, ou sans fin.")]
    public int? DurationMinutes { get; set; }
}

public class PickupSlotDto
{
    [Required] public DateTimeOffset Start { get; set; }
    [Required] public DateTimeOffset End { get; set; }
    [Required] public int Remaining { get; set; }
}

/// <summary>Paramètres modifiables par le Manager (US-E10-5, US-E03-5/6).</summary>
public class RestaurantSettingsWrite
{
    [Required(ErrorMessage = "Le nom commercial est obligatoire.")]
    [MaxLength(128)]
    public string TradeName { get; set; } = string.Empty;

    [MaxLength(128)] public string? LegalName { get; set; }
    [MaxLength(256)] public string? Address { get; set; }
    [MaxLength(32)] [Phone] public string? Phone { get; set; }
    [Siret] public string? Siret { get; set; }

    [MaxLength(16)]
    [RegularExpression("^FR[0-9A-Z]{2}[0-9]{9}$", ErrorMessage = "N° de TVA intracommunautaire invalide (FRxx123456789).")]
    public string? VatNumber { get; set; }

    [Range(0, 240)] public int AsapDelayMinutes { get; set; } = 15;
    [Range(5, 120)] public int SlotDurationMinutes { get; set; } = 15;
    [Range(1, 500)] public int SlotCapacityOrders { get; set; } = 5;
    [Range(0, 240)] public int MinLeadMinutes { get; set; } = 20;
    [Range(1, 120)] public int ApprovalTimeoutMinutes { get; set; } = 10;
    [Range(0, 240)] public int PickupGraceMinutes { get; set; } = 30;
    [Range(1, 20)] public int NoShowThreshold { get; set; } = 2;
    [Range(5, 30)] public int OnlinePaymentExpiryMinutes { get; set; } = 30;
}

public class RestaurantSettingsDetails : RestaurantSettingsWrite
{
    public string? LogoUrl { get; set; }
    [Required] public bool IsPaused { get; set; }
    public DateTimeOffset? PausedUntil { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class RestaurantSettingsHistoryItem
{
    [Required] public Guid Id { get; set; }
    [Required] public DateTimeOffset ChangedAt { get; set; }
    [Required] public Guid ChangedByUserId { get; set; }
    public string? ChangedByName { get; set; }

    /// <summary>Paramètres après la modification (JSON).</summary>
    [Required] public string Snapshot { get; set; } = "{}";
}

/// <summary>Informations publiques (PWA, reçus).</summary>
public class RestaurantInfo
{
    [Required] public string TradeName { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Siret { get; set; }
    public string? VatNumber { get; set; }
    public string? LogoUrl { get; set; }
    [Required] public List<OpeningRangeDto> Hours { get; set; } = [];
    [Required] public List<ClosureDetails> UpcomingClosures { get; set; } = [];
}
