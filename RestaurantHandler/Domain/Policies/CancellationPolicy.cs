using RestaurantHandler.Models;

namespace RestaurantHandler.Utilities;

/// <summary>
/// LA règle d'annulation — source de vérité unique de « qui paie quoi » quand une réservation
/// PAYÉE est annulée. Même esprit que <c>OrderAccountingService.BuildAccounting</c> pour la
/// compta : la répartition est calculée ici et nulle part ailleurs, pour que l'endpoint de prévisualisation
/// annonce à l'élève exactement ce que l'annulation exécutera.
///
/// <para>
/// Tout se dérive de <see cref="Reservation.Amount"/> et de <see cref="Reservation.CommissionRate"/>
/// (figée à la réservation) : aucune dépendance à <c>Payment.StripeFee</c>, qui est backfillé de
/// façon différée et pourrait ne pas être connu au moment de l'annulation.
/// </para>
///
/// <list type="bullet">
/// <item>Élève, ≥ 48h avant le cours : remboursé au brut. La plateforme absorbe les frais Stripe
/// (Stripe ne les restitue pas sur un remboursement) — c'est le coût du service.</item>
/// <item>Élève, &lt; 48h avant le cours : remboursé price*(1-c). La plateforme garde sa commission,
/// qui couvre les frais Stripe. Le prof, lui, ne touche rien (son crédit est annulé).</item>
/// <item>Prof : l'élève est remboursé au brut et le prof porte le coût — pénalité de 5 % inscrite
/// en dette sur son wallet, recouvrée sur ses versements suivants.</item>
/// </list>
/// </summary>
public static class CancellationPolicy
{
    /// <summary>
    /// Répartition d'une annulation. Les trois montants sont positifs ; c'est l'appelant qui pose
    /// le signe (la réversion et la pénalité sont écrites en négatif au ledger).
    /// </summary>
    /// <param name="RefundAmount">Remboursé à l'élève (ce que Stripe rend).</param>
    /// <param name="RetainedAmount">Ce que l'élève ne récupère pas (= Amount - RefundAmount).</param>
    /// <param name="TeacherReversal">Crédit prof annulé (toujours la totalité de sa part).</param>
    /// <param name="TeacherPenalty">Dette mise à la charge du prof (0 sauf annulation prof).</param>
    public readonly record struct CancellationOutcome(
        decimal RefundAmount,
        decimal RetainedAmount,
        decimal TeacherReversal,
        decimal TeacherPenalty
    );

    /// <summary>
    /// Une annulation est « tardive » quand le cours démarre dans moins de
    /// <see cref="HardCode.LATE_CANCELLATION_WINDOW"/>.
    /// </summary>
    public static bool IsLate(Slot slot, DateTimeOffset now) =>
        slot.DateFrom <= now + HardCode.LATE_CANCELLATION_WINDOW;

    /// <summary>Annulation par l'élève. Le prof ne touche rien dans les deux cas.</summary>
    public static CancellationOutcome ForStudent(Reservation reservation, DateTimeOffset now)
    {
        var teacherShare = TeacherShare(reservation);
        var refund = IsLate(reservation.Slot!, now) ? teacherShare : reservation.Amount;

        return new CancellationOutcome(
            RefundAmount: refund,
            RetainedAmount: reservation.Amount - refund,
            TeacherReversal: teacherShare,
            TeacherPenalty: 0m
        );
    }

    /// <summary>
    /// Annulation par le prof : l'élève est remboursé intégralement, quel que soit le délai, et le
    /// prof paie la pénalité. Pas de seuil de temps ici — un prof qui annule reste responsable.
    /// </summary>
    public static CancellationOutcome ForTeacher(Reservation reservation)
    {
        return new CancellationOutcome(
            RefundAmount: reservation.Amount,
            RetainedAmount: 0m,
            TeacherReversal: TeacherShare(reservation),
            TeacherPenalty: Round(reservation.Amount * HardCode.TEACHER_CANCELLATION_FEE_RATE)
        );
    }

    // Part du prof telle qu'elle a été créditée au paiement (PaymentService), au centime près :
    // la réversion doit annuler exactement le crédit, sinon un résidu traîne au ledger.
    private static decimal TeacherShare(Reservation reservation) =>
        Round(reservation.Amount * (1m - reservation.CommissionRate));

    private static decimal Round(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
