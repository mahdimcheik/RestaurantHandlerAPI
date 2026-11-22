namespace RestaurantHandler.Utilities;

/// <summary>
/// Graphe des transitions permises d'une réservation, copie exacte de
/// <c>docs/20-specs-fonctionnelles/02-machines-etats.md</c>. Ne porte que le graphe : les
/// conditions temporelles (fenêtre de litige, début du créneau…) restent dans les services.
/// </summary>
public static class ReservationStateMachine
{
    private static readonly Dictionary<Guid, Guid[]> Allowed = new()
    {
        [HardCode.RESERVATION_PENDING] =
        [
            HardCode.RESERVATION_ACCEPTED,
            HardCode.RESERVATION_REJECTED,
            HardCode.RESERVATION_CANCELLED,
        ],
        [HardCode.RESERVATION_ACCEPTED] =
        [
            HardCode.RESERVATION_DELIVERED,
            HardCode.RESERVATION_CANCELLED,
            HardCode.RESERVATION_DISPUTED,
            HardCode.RESERVATION_PAYABLE,
            HardCode.RESERVATION_DONE,
        ],
        [HardCode.RESERVATION_DELIVERED] =
        [
            HardCode.RESERVATION_PAYABLE,
            HardCode.RESERVATION_DISPUTED,
        ],
        [HardCode.RESERVATION_DISPUTED] =
        [
            HardCode.RESERVATION_PAYABLE,
            HardCode.RESERVATION_CANCELLED,
        ],
        [HardCode.RESERVATION_PAYABLE] = [HardCode.RESERVATION_DONE],
    };

    /// <summary>
    /// <c>true</c> si <paramref name="from"/> → <paramref name="to"/> est une arête du graphe.
    /// <c>ACCEPTED → DONE</c> n'existe qu'en flux sans paiement : une réservation payée
    /// n'atteint <c>DONE</c> que par <c>PAYABLE</c>, seule la tâche de versement pouvant
    /// affirmer que le professeur est versé.
    /// </summary>
    /// <param name="paymentRequired">
    /// Flux de la commande ; <c>null</c> si inconnu, ce qui refuse l'arête conditionnelle.
    /// </param>
    public static bool CanTransition(Guid from, Guid to, bool? paymentRequired)
    {
        if (!Allowed.TryGetValue(from, out var targets) || !targets.Contains(to))
            return false;

        if (from == HardCode.RESERVATION_ACCEPTED && to == HardCode.RESERVATION_DONE)
            return paymentRequired == false;

        return true;
    }

    public static void EnsureCanTransition(Guid from, Guid to, bool? paymentRequired)
    {
        if (!CanTransition(from, to, paymentRequired))
            throw new InvalidReservationTransitionException(from, to);
    }
}

public class InvalidReservationTransitionException(Guid from, Guid to)
    : InvalidOperationException($"Transition de réservation interdite : {from} → {to}")
{
    public Guid From { get; } = from;
    public Guid To { get; } = to;
}
