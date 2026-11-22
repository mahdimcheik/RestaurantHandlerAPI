using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models;

public class SlotDetails
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public DateTimeOffset DateFrom { get; set; }

    [Required]
    public DateTimeOffset DateTo { get; set; }

    public TeacherDetails? Teacher { get; set; }

    [Required]
    public Guid TypeId { get; set; }

    public TypeSlotDetails? Type { get; set; }

    public ReservationDetails Reservation { get; set; }

    public SlotDetails() { }

    public SlotDetails(Slot slot, bool includeTeacherEmail = false)
    {
        Id = slot.Id;
        DateFrom = slot.DateFrom;
        DateTo = slot.DateTo;
        TypeId = slot.TypeId ?? Guid.Empty;

        if (slot.Reservations != null && slot.Reservations.Count > 0)
        {
            Reservation = new ReservationDetails(slot.Reservations.First());
        }

        if (slot.Teacher != null)
        {
            Teacher = new TeacherDetails(slot.Teacher, includeTeacherEmail);
        }

        if (slot.Type != null)
        {
            Type = new TypeSlotDetails(slot.Type);
        }
    }
}


public class SlotMinimalDetails
{
    [Required]
    public Guid Id { get; set; }
    [Required]
    public DateTimeOffset DateFrom { get; set; }
    [Required]
    public DateTimeOffset DateTo { get; set; }
    public TeacherDetails? Teacher { get; set; }
    [Required]
    public Guid TypeId { get; set; }
    public TypeSlotDetails? Type { get; set; }

    public SlotMinimalDetails() { }

    public SlotMinimalDetails(Slot slot, bool includeTeacherEmail = false)
    {
        Id = slot.Id;
        DateFrom = slot.DateFrom;
        DateTo = slot.DateTo;
        TypeId = slot.TypeId ?? Guid.Empty;

        if (slot.Teacher != null)
        {
            Teacher = new TeacherDetails(slot.Teacher, includeTeacherEmail);
        }

        if (slot.Type != null)
        {
            Type = new TypeSlotDetails(slot.Type);
        }
    }
}

public class SlotCreate
{
    [Required(ErrorMessage = "La date de début est requise")]
    public DateTimeOffset DateFrom { get; set; }
    [Required(ErrorMessage = "La date de fin est requise")]
    public DateTimeOffset DateTo { get; set; }
    [Required(ErrorMessage = "L'identifiant de l'enseignant est requis")]
    public Guid TeacherId { get; set; }
    [Required(ErrorMessage = "L'identifiant du type de créneau est requis")]
    public Guid TypeId { get; set; }
}

public class SlotUpdate
{
    [Required]
    public Guid Id { get; set; }

    [Required(ErrorMessage = "La date de début est requise")]
    public DateTimeOffset DateFrom { get; set; }

    [Required(ErrorMessage = "La date de fin est requise")]
    public DateTimeOffset DateTo { get; set; }

    /// <summary>
    /// Ignoré : un créneau reste au professeur qui l'a créé. Conservé pour le client généré.
    /// </summary>
    [Required(ErrorMessage = "L'identifiant de l'enseignant est requis")]
    public Guid TeacherId { get; set; }

    [Required(ErrorMessage = "L'identifiant du type de créneau est requis")]
    public Guid TypeId { get; set; }

    public void UpdateSlot(Slot slot)
    {
        slot.DateFrom = DateFrom;
        slot.DateTo = DateTo;
        slot.TypeId = TypeId;
        slot.UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public class SlotPeriod
{
    [Required(ErrorMessage = "La date de début est requise")]
    public DateTimeOffset DateFrom { get; set; }

    [Required(ErrorMessage = "La date de fin est requise")]
    public DateTimeOffset DateTo { get; set; }

    /// <summary>Type propre à ce créneau (copier-coller) ; sinon celui du lot.</summary>
    public Guid? TypeId { get; set; }
}

/// <summary>
/// Créneaux répétés ou collés : les occurrences sont calculées par le client, en heure locale.
/// </summary>
public class SlotBatchCreate
{
    /// <summary>Type par défaut des créneaux qui n'en précisent pas.</summary>
    public Guid? TypeId { get; set; }

    [Required(ErrorMessage = "Au moins un créneau est requis")]
    [MinLength(1, ErrorMessage = "Au moins un créneau est requis")]
    [MaxLength(SlotBatchLimits.MaxSlots, ErrorMessage = "Trop de créneaux en une fois")]
    public List<SlotPeriod> Slots { get; set; } = [];
}

public static class SlotBatchLimits
{
    public const int MaxSlots = 200;
    public const int MaxSpanDays = 186;
}

public class SlotBatchSkipped
{
    [Required]
    public DateTimeOffset DateFrom { get; set; }

    [Required]
    public DateTimeOffset DateTo { get; set; }

    /// <summary>overlap, past ou invalid.</summary>
    [Required]
    public string Reason { get; set; } = string.Empty;
}

public class SlotBatchResult
{
    [Required]
    public int CreatedCount { get; set; }

    [Required]
    public List<SlotBatchSkipped> Skipped { get; set; } = [];
}
