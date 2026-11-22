namespace RestaurantHandler.Models;

/// <summary>Levée du blocage « paiement au retrait » d'un client par un Manager (US-E10-4).</summary>
public class NoShowReset
{
    public long Id { get; set; }

    public Guid UserId { get; set; }
    public UserApp? User { get; set; }

    /// <summary>Compteur de non-venues avant la remise à zéro.</summary>
    public int PreviousCount { get; set; }

    public Guid ResetByUserId { get; set; }
    public UserApp? ResetByUser { get; set; }

    public DateTimeOffset ResetAt { get; set; }
}
