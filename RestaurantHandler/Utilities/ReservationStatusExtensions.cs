using RestaurantHandler.Contexts;
using RestaurantHandler.Models;

namespace RestaurantHandler.Utilities;

/// <summary>
/// Point d'entrée unique pour faire évoluer le statut d'une réservation.
/// Met à jour le statut courant dénormalisé (<see cref="Reservation.StatusId"/>,
/// lu sur le chemin paiement) ET enregistre une ligne d'audit dans
/// <c>ReservationStatusHistories</c>, dans la même unité de travail.
///
/// Ces méthodes ne sauvegardent volontairement PAS : l'appelant garde la main sur
/// la transaction (<c>SaveChangesAsync</c>) pour que le changement de statut et son
/// log soient persistés atomiquement. Toute mutation de <see cref="Reservation.StatusId"/>
/// doit passer par ici, sinon l'historique se désynchronise du statut courant.
/// </summary>
public static class ReservationStatusExtensions
{
    /// <summary>
    /// Enregistre la transition initiale (création) : <c>null → statut courant</c>.
    /// À appeler juste après <c>new Reservation(...)</c>, avant le premier
    /// <c>SaveChangesAsync</c>.
    /// </summary>
    public static ReservationStatusHistory RecordStatusCreation(
        this Reservation reservation,
        Guid? changedById = null,
        string? reason = null)
    {
        var history = new ReservationStatusHistory
        {
            // Navigation plutôt que ReservationId : pour une réservation pas encore
            // persistée, EF résout la FK à la sauvegarde (l'Id peut être vide ici).
            Reservation = reservation,
            FromStatusId = null,
            ToStatusId = reservation.StatusId,
            ChangedById = changedById,
            Reason = reason,
        };

        reservation.StatusHistory.Add(history);
        return history;
    }

    /// <summary>
    /// Transitionne la réservation vers <paramref name="newStatusId"/> : met à jour
    /// le statut courant + <c>UpdatedAt</c> et journalise la transition
    /// (<c>ancien → nouveau</c>). No-op si le statut ne change pas (l'historique
    /// n'est pas pollué). Renvoie la ligne créée, ou <c>null</c> si no-op.
    /// Lève <see cref="InvalidReservationTransitionException"/> si la transition sort du
    /// graphe (<see cref="ReservationStateMachine"/>) ; <c>ACCEPTED → DONE</c> exige que
    /// <see cref="Reservation.Order"/> soit chargée.
    /// </summary>
    /// <param name="changedById">Acteur de la transition ; <c>null</c> = système (cron, webhook).</param>
    public static ReservationStatusHistory? TransitionStatusTo(
        this Reservation reservation,
        MainContext context,
        Guid newStatusId,
        Guid? changedById = null,
        string? reason = null)
    {
        if (reservation.StatusId == newStatusId)
            return null;

        ReservationStateMachine.EnsureCanTransition(
            reservation.StatusId,
            newStatusId,
            reservation.Order?.PaymentRequired
        );

        var history = new ReservationStatusHistory
        {
            Reservation = reservation,
            FromStatusId = reservation.StatusId,
            ToStatusId = newStatusId,
            ChangedById = changedById,
            Reason = reason,
        };

        reservation.StatusId = newStatusId;
        reservation.UpdatedAt = DateTimeOffset.UtcNow;
        reservation.StatusHistory.Add(history);
        context.ReservationStatusHistories.Add(history);

        return history;
    }
}
