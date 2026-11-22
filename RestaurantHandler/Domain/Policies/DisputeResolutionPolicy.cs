using RestaurantHandler.Models;

namespace RestaurantHandler.Utilities;

/// <summary>
/// LA règle de résolution d'un litige — même rôle que <see cref="CancellationPolicy"/> : l'issue,
/// le montant remboursé, la part retirée au professeur et l'état de sortie de la réservation sont
/// calculés ici et nulle part ailleurs, pour toutes les voies de résolution.
/// <para>
/// Paramétrée par R, le montant brut remboursé à l'élève, borné à [0, Amount] :
/// </para>
/// <list type="bullet">
/// <item><c>R == 0</c> → issue professeur, réservation <c>PAYABLE</c> ;</item>
/// <item><c>R == Amount</c> → issue élève, réservation <c>CANCELLED</c> ;</item>
/// <item>entre les deux → issue partielle, réservation <c>PAYABLE</c> pour verser le reste.</item>
/// </list>
/// <para>
/// Le remboursement est à la charge du professeur : son crédit est annulé de
/// <c>min(R, part professeur)</c> et la plateforme garde sa commission. Le plafond fait qu'une
/// issue partielle portant sur la totalité du montant donne le même résultat qu'une issue élève,
/// et qu'une résolution ne crée jamais de dette (I14).
/// </para>
/// </summary>
public static class DisputeResolutionPolicy
{
    /// <param name="OutcomeId">Issue du litige (<c>DISPUTE_OUTCOME_*</c>).</param>
    /// <param name="RefundAmount">Remboursé à l'élève.</param>
    /// <param name="TeacherReversal">Crédit prof annulé, positif ; l'appelant l'écrit en négatif.</param>
    /// <param name="ReservationStatusId">État de la réservation en sortie de <c>DISPUTED</c>.</param>
    public readonly record struct DisputeResolution(
        Guid OutcomeId,
        decimal RefundAmount,
        decimal TeacherReversal,
        Guid ReservationStatusId
    );

    /// <summary>Null si le montant demandé sort de [0, Amount].</summary>
    public static DisputeResolution? Resolve(Reservation reservation, decimal? requestedRefund)
    {
        var refund = Round(requestedRefund ?? 0m);
        if (refund < 0m || refund > reservation.Amount)
            return null;

        var isFullRefund = refund == reservation.Amount;
        var teacherShare = Round(reservation.Amount * (1m - reservation.CommissionRate));

        return new DisputeResolution(
            OutcomeId: refund == 0m ? HardCode.DISPUTE_OUTCOME_TEACHER
                : isFullRefund ? HardCode.DISPUTE_OUTCOME_STUDENT
                : HardCode.DISPUTE_OUTCOME_PARTIAL,
            RefundAmount: refund,
            TeacherReversal: Math.Min(refund, teacherShare),
            // Jamais DONE : DONE signifie « professeur versé » et seule la tâche de versement
            // peut l'affirmer.
            ReservationStatusId: isFullRefund
                ? HardCode.RESERVATION_CANCELLED
                : HardCode.RESERVATION_PAYABLE
        );
    }

    private static decimal Round(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
