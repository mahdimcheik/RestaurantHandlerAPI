using System.Linq.Expressions;
using RestaurantHandler.Models;

namespace RestaurantHandler.Utilities;

/// <summary>
/// Définition unique de « ce créneau est réservable », utilisée par la recherche de profs.
///
/// <para>
/// <b>Pourquoi un seul endroit.</b> La recherche par semaine s'appuie sur ce prédicat
/// trois fois dans la même requête — pour <i>filtrer</i> les profs qui ont une dispo
/// (<c>EXISTS</c>), pour les <i>trier</i> par dispo la plus proche (<c>min(DateFrom)</c>)
/// et pour <i>compter</i> les créneaux libres affichés. Si ces trois copies divergeaient,
/// un prof pourrait être listé sur une disponibilité que l'élève ne peut pas réserver, ou
/// affiché avec un compte qui ne correspond pas à son classement. Même raison d'être que
/// <see cref="CancellationPolicy"/> : une règle, un fichier, partagé entre les chemins.
/// </para>
/// </summary>
public static class TeacherAvailability
{
    /// <summary>
    /// Statuts de réservation qui <b>occupent</b> un créneau (règle R1) : toute réservation
    /// vivante. Seuls <c>CANCELLED</c> et <c>REJECTED</c> libèrent le créneau. Définition
    /// unique, partagée par la recherche, la réservation (<c>BookingService</c>), les
    /// gardes de modification/suppression et les calendriers (<c>SlotsService</c>) et la purge
    /// des créneaux passés (<c>SlotCleaningJob</c>). S'utilise sous la forme
    /// <c>BlockingStatuses.Contains(r.StatusId)</c>, qu'EF traduit en SQL.
    /// </summary>
    public static readonly Guid[] BlockingStatuses =
    [
        HardCode.RESERVATION_PENDING,
        HardCode.RESERVATION_ACCEPTED,
        HardCode.RESERVATION_DELIVERED,
        HardCode.RESERVATION_PAYABLE,
        HardCode.RESERVATION_DISPUTED,
        HardCode.RESERVATION_DONE,
    ];

    /// <summary>
    /// « Ce créneau est réservable entre <paramref name="from"/> et <paramref name="to"/> » :
    /// non archivé, dans la plage, et sans réservation vivante bloquante. Traduit en SQL —
    /// à utiliser tel quel, ne pas recopier le prédicat.
    /// </summary>
    /// <param name="from">
    /// Borne basse, <b>toujours &gt;= maintenant</b> côté appelant : un créneau passé n'est
    /// pas une disponibilité, même s'il n'a jamais été réservé. Sans ce plancher, un prof
    /// ayant laissé traîner de vieux créneaux libres remonterait indéfiniment en tête.
    /// </param>
    /// <param name="to">Borne haute optionnelle : la fin de la semaine recherchée.</param>
    public static Expression<Func<Slot, bool>> IsFree(
        DateTimeOffset from,
        DateTimeOffset? to = null
    ) =>
        s =>
            s.ArchivedAt == null
            && s.DateFrom >= from
            && (to == null || s.DateTo <= to)
            && !s.Reservations.Any(r =>
                r.ArchivedAt == null && BlockingStatuses.Contains(r.StatusId)
            );
}
