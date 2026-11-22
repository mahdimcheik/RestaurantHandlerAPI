using RestaurantHandler.Contexts;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

public class ReservationCancellationService(
    MainContext context,
    NotificationDispatcher notifications,
    ReservationCartService cart
)
{
    public async Task RemoveReservationByTeacherAsync(
        Guid reservationId,
        CallerIdentity? principals
    )
    {
        var teacher = CheckUser.GetUser(principals, context);
        if (teacher is null)
        {
            throw new ResourceNotFoundException("Utilisateur non trouvé");
        }
        var reservation = await context
            .Reservations.Where(r =>
                r.Id == reservationId
                && r.ArchivedAt == null
                && (
                    r.StatusId == HardCode.RESERVATION_PENDING
                    || r.StatusId == HardCode.RESERVATION_ACCEPTED
                )
            )
            .Include(r => r.Slot)
            .Include(r => r.Student.User)
            .Where(r => r.Slot.TeacherId == teacher.Id)
            .FirstOrDefaultAsync();

        if (reservation is null)
        {
            throw new ResourceNotFoundException("Réservation non trouvée");
        }

        if (reservation.StatusId == HardCode.RESERVATION_ACCEPTED)
        {
            // Réservation déjà validée. Le prof ne peut l'annuler que TANT QUE le cours
            // n'est pas terminé (DateTo > now) : le payout part 48h après la fin du
            // créneau, donc avant la fin le crédit est forcément encore gelé
            // (PayoutId == null) et aucun Transfer Reversal / clawback n'est requis.
            if (reservation.Slot.DateTo <= DateTimeOffset.UtcNow)
            {
                throw new BusinessConflictException("Cours déjà terminé : annulation impossible");
            }

            var order = await context
                .Orders.Where(o => o.Id == reservation.OrderId)
                .Include(o => o.Reservations)
                .Include(o => o.Payment)
                .FirstOrDefaultAsync();

            if (order is null)
            {
                throw new InvalidOperationException("Commande introuvable");
            }

            // Commande sans paiement : aucun argent n'a jamais transité, donc aucune
            // conséquence financière — simple transition, pas de Refund/pénalité, et la
            // commande reste PENDING (REFUNDED signifie qu'un Refund a été émis).
            if (!order.PaymentRequired)
            {
                await CancelNoPaymentReservationAsync(
                    reservation,
                    order,
                    teacher.Id,
                    "Annulée par le prof",
                    reservation.StudentId,
                    reservation.Student.User.Email
                );
                return;
            }

            // Commande payée = IMMUABLE : on ne supprime pas la ligne et on ne touche
            // jamais TotalAmount / Payment.Amount. Comme côté élève, le remboursement
            // Stripe n'est PAS émis inline : on enregistre l'intention (ligne Refund en
            // PENDING) et le cron `ProcessPendingRefunds` l'exécute 48h plus tard.
            if (order.Payment?.TransactionRef is null)
            {
                throw new InvalidOperationException("Paiement introuvable : remboursement impossible");
            }

            // C'est le prof qui annule : l'élève est remboursé INTÉGRALEMENT (frais Stripe
            // compris, quel que soit le délai) et le prof porte le coût sous forme d'une
            // pénalité inscrite en dette sur son wallet (cf. ApplyPaidCancellation).
            ApplyPaidCancellation(
                reservation,
                order,
                teacher.Id,
                "Annulée par le prof",
                CancellationPolicy.ForTeacher(reservation)
            );
        }
        else if (reservation.StatusId == HardCode.RESERVATION_PENDING)
        {
            // Réservation pas encore payée : rien à rembourser, elle passe REJECTED. Mais la
            // ligne comptait dans le total du panier (comme côté élève, RemoveReservationByStudentAsync) —
            // il faut le réajuster, sans quoi une commande sans paiement (visible tant
            // qu'elle contient une réservation active) afficherait un total erroné.
            var pendingOrder = await context
                .Orders.Where(o => o.Id == reservation.OrderId)
                .Include(o => o.Reservations)
                .FirstOrDefaultAsync();

            cart.WithdrawFromOrder(
                reservation,
                pendingOrder,
                HardCode.RESERVATION_REJECTED,
                teacher.Id,
                "Refusée par le prof",
                DateTimeOffset.UtcNow
            );

            // La commande a changé : tout checkout ouvert est désormais périmé (même
            // traitement que côté élève, RemoveReservationByStudentAsync).
            if (pendingOrder != null)
            {
                await cart.ExpireOpenCheckoutAsync(pendingOrder);
            }
        }

        await context.SaveChangesAsync();

        await notifications.NotifyAsync(
            new NotificationRequest(
                reservation.StudentId,
                reservation.Student.User.Email,
                NotificationTypeEnum.Reservation,
                "Votre réservation a été annulée par le professeur",
                "#ffabcf",
                "pi pi-minus",
                NotificationLinks.Reservation(reservation.Id, forTeacher: false)
            )
        );
    }

    /// <summary>
    /// Ce que l'élève récupérerait s'il annulait maintenant — à afficher avant de confirmer.
    /// Passe par <see cref="CancellationPolicy"/>, exactement comme
    /// <see cref="RemoveReservationByStudentAsync"/> : l'annonce et l'exécution ne peuvent pas
    /// diverger. Un chouïa de temps s'écoule entre les deux appels : si l'élève confirme à
    /// cheval sur la limite des 48h, c'est le montant calculé à l'annulation qui fait foi.
    /// </summary>
    public async Task<CancellationPreview> GetCancellationPreviewAsync(
        Guid reservationId,
        CallerIdentity? principals
    )
    {
        var student = CheckUser.GetUser(principals, context);
        if (student is null)
        {
            throw new ResourceNotFoundException("Utilisateur non trouvé");
        }

        var reservation = await context
            .Reservations.Where(r =>
                r.Id == reservationId
                && r.ArchivedAt == null
                && r.StudentId == student.Id
                && (
                    r.StatusId == HardCode.RESERVATION_ACCEPTED
                    || r.StatusId == HardCode.RESERVATION_PENDING
                )
            )
            .Include(r => r.Slot)
            .Include(r => r.Order)
            .FirstOrDefaultAsync();

        if (reservation?.Slot is null)
        {
            throw new ResourceNotFoundException("Réservation non trouvée");
        }

        if (reservation.StatusId == HardCode.RESERVATION_PENDING)
        {
            return new CancellationPreview
            {
                Amount = reservation.Amount,
                RefundAmount = reservation.Amount,
                RetainedAmount = 0,
                IsLate = false,
                LateCancellationDeadline = reservation.Slot.DateFrom,
                CanCancel = true,
            };
        }

        var now = DateTimeOffset.UtcNow;

        // Réservation validée mais sans paiement : rien n'a été encaissé, donc rien à
        // retenir — annulable gratuitement jusqu'au début du cours, comme une réservation
        // encore PENDING (même cutoff que le flux payant : après DateFrom, litige/contact
        // direct, pas d'annulation).
        if (reservation.Order?.PaymentRequired == false)
        {
            return new CancellationPreview
            {
                Amount = reservation.Amount,
                RefundAmount = reservation.Amount,
                RetainedAmount = 0,
                IsLate = false,
                LateCancellationDeadline = reservation.Slot.DateFrom,
                CanCancel = reservation.Slot.DateFrom > now,
            };
        }

        var outcome = CancellationPolicy.ForStudent(reservation, now);

        return new CancellationPreview
        {
            Amount = reservation.Amount,
            RefundAmount = outcome.RefundAmount,
            RetainedAmount = outcome.RetainedAmount,
            IsLate = CancellationPolicy.IsLate(reservation.Slot, now),
            LateCancellationDeadline =
                reservation.Slot.DateFrom - HardCode.LATE_CANCELLATION_WINDOW,
            CanCancel = reservation.Slot.DateFrom > now,
        };
    }

    public async Task RemoveReservationByStudentAsync(
        Guid reservationId,
        CallerIdentity? principals
    )
    {
        var student = CheckUser.GetUser(principals, context);
        if (student is null)
        {
            throw new ResourceNotFoundException("Utilisateur non trouvé");
        }
        var reservation = await context
            .Reservations.Where(r =>
                r.Id == reservationId
                && r.ArchivedAt == null
                && r.StudentId == student.Id
                && (
                    r.StatusId == HardCode.RESERVATION_PENDING
                    || r.StatusId == HardCode.RESERVATION_ACCEPTED
                )
            )
            .Include(r => r.Slot)
            .ThenInclude(s => s.Teacher.User)
            .FirstOrDefaultAsync();

        if (reservation is null)
        {
            throw new ResourceNotFoundException("Réservation non trouvée");
        }

        var now = DateTimeOffset.UtcNow;

        // Une réservation PAYÉE n'est plus annulable une fois le cours COMMENCÉ : après
        // DateFrom, la seule voie est le litige (Feature 8), qui tranche si le cours a bien
        // eu lieu. Avant DateFrom, l'annulation est toujours possible — c'est le montant
        // remboursé qui varie (cf. CancellationPolicy : intégral au-delà de 48h, price*(1-c)
        // en deçà). Une réservation PENDING n'est qu'un article du panier : rien n'a été
        // payé, on peut la retirer à tout moment.
        if (
            reservation.StatusId == HardCode.RESERVATION_ACCEPTED
            && reservation.Slot.DateFrom <= now
        )
        {
            throw new BusinessConflictException("Cours commencé : annulation impossible (ouvrez un litige)");
        }

        var order = await context
            .Orders.Where(o => o.Id == reservation.OrderId)
            .Include(o => o.Reservations)
            .Include(o => o.Payment)
            .FirstOrDefaultAsync();

        if (reservation.StatusId == HardCode.RESERVATION_PENDING)
        {
            // Réservation pas encore payée : elle passe CANCELLED et quitte le panier, et on
            // réajuste le total de la commande à partir du snapshot de prix figé
            // (Amount), source de vérité unique — jamais recalculé depuis Cursus.
            cart.WithdrawFromOrder(
                reservation,
                order,
                HardCode.RESERVATION_CANCELLED,
                student.Id,
                "Retirée du panier par l'élève",
                now
            );
            await context.SaveChangesAsync();

            // La commande a changé : tout checkout ouvert est désormais périmé.
            if (order != null)
            {
                await cart.ExpireOpenCheckoutAsync(order);
            }
        }
        else
        {
            if (order is null)
            {
                throw new InvalidOperationException("Commande introuvable");
            }

            // Commande sans paiement : rien n'a jamais été encaissé, donc rien à
            // rembourser — simple transition, pas de Refund/pénalité, et la commande
            // reste PENDING (REFUNDED signifie qu'un Refund a été émis).
            if (!order.PaymentRequired)
            {
                await CancelNoPaymentReservationAsync(
                    reservation,
                    order,
                    student.Id,
                    "Annulée par l'élève",
                    reservation.Slot.TeacherId,
                    reservation.Slot.Teacher.User.Email
                );
                return;
            }

            // Réservation déjà payée (ACCEPTED). Une commande payée est IMMUABLE :
            // on ne supprime pas la ligne et on ne touche jamais à TotalAmount /
            // Payment.Amount. L'annulation est un évènement ajouté par-dessus :
            // transition CANCELLED (garde la ligne + journalise l'historique),
            // remboursement Stripe à l'élève, écriture ledger négative côté prof,
            // et statut de commande REFUNDED / REFUNDED_PARTIALLY (cf. ApplyPaidCancellation).
            //
            // Le remboursement Stripe n'est PAS émis inline : on enregistre seulement
            // l'intention (ligne Refund en PENDING) et le cron `ProcessPendingRefunds`
            // l'exécute 48h plus tard — la même fenêtre différée que le payout prof,
            // pour qu'aucun renversement de dernière minute ne course un remboursement
            // synchrone, et pour ne pas coupler la requête d'annulation à Stripe.
            //
            // L'annulation n'étant permise qu'AVANT le cours et le payout n'ayant lieu
            // que 48h APRÈS, le crédit wallet du prof est forcément encore gelé
            // (PayoutId == null) : aucun argent n'est parti vers le prof, donc pas de
            // Transfer Reversal à faire ici.
            if (order.Payment?.TransactionRef is null)
            {
                throw new InvalidOperationException("Paiement introuvable : remboursement impossible");
            }

            // Combien l'élève récupère : intégral s'il s'y prend à plus de 48h du cours,
            // sinon price*(1-c) — la plateforme garde sa commission, qui couvre les frais
            // Stripe (que Stripe ne restitue pas). Dans les deux cas le prof ne touche rien.
            var outcome = CancellationPolicy.ForStudent(reservation, now);
            var isLate = outcome.RetainedAmount > 0m;
            var reason = isLate
                ? $"Annulée par l'élève (tardive : {outcome.RetainedAmount:0.00} € retenus)"
                : "Annulée par l'élève";

            ApplyPaidCancellation(reservation, order, student.Id, reason, outcome);

            await context.SaveChangesAsync();
        }

        await notifications.NotifyAsync(
            new NotificationRequest(
                reservation.Slot.TeacherId,
                reservation.Slot.Teacher.User.Email,
                NotificationTypeEnum.Reservation,
                "Une réservation vient d'être annulée par l'élève",
                "#ffabcf",
                "pi pi-minus",
                NotificationLinks.Reservation(reservation.Id, forTeacher: true)
            )
        );
    }

    // No-payment order (Order.PaymentRequired == false): nothing was ever charged, so
    // cancelling either side is a pure notify-and-cancel — no Refund, no wallet write,
    // and the order itself stays PENDING (REFUNDED means a Refund was actually issued).
    private async Task CancelNoPaymentReservationAsync(
        Reservation reservation,
        Order order,
        Guid cancelledBy,
        string reason,
        Guid notifyUserId,
        string notifyEmail
    )
    {
        var now = DateTimeOffset.UtcNow;
        cart.WithdrawFromOrder(
            reservation,
            order,
            HardCode.RESERVATION_CANCELLED,
            cancelledBy,
            reason,
            now
        );
        order.UpdatedAt = now;

        await notifications.NotifyAsync(
            new NotificationRequest(
                notifyUserId,
                notifyEmail,
                NotificationTypeEnum.Reservation,
                "Une réservation vient d'être annulée",
                "#ffabcf",
                "pi pi-minus",
                NotificationLinks.Reservation(reservation.Id, forTeacher: notifyUserId == reservation.Slot.TeacherId)
            )
        );
    }

    // Paid order (Order.PaymentRequired == true), reservation cancelled before the slot
    // started: the order is IMMUTABLE (TotalAmount/Payment.Amount never change) — the
    // cancellation is an event recorded on top. Transition to CANCELLED (keeps the row +
    // history), a PENDING Refund (executed 48h later by ProcessPendingRefunds), the
    // matching wallet reversal/penalty, and the order status.
    private void ApplyPaidCancellation(
        Reservation reservation,
        Order order,
        Guid cancelledBy,
        string reason,
        CancellationPolicy.CancellationOutcome outcome
    )
    {
        reservation.TransitionStatusTo(context, HardCode.RESERVATION_CANCELLED, cancelledBy, reason);

        context.Refunds.Add(
            new Refund
            {
                Amount = outcome.RefundAmount,
                ReservationId = reservation.Id,
                OrderId = order.Id,
                PaymentId = order.Payment!.Id,
                StripeRefundId = null,
                Reason = reason,
                StatusId = HardCode.STATUS_TRANSACTION_PENDING,
            }
        );

        if (outcome.TeacherReversal > 0m)
        {
            context.TeacherWalletTransactions.Add(
                new TeacherWalletTransaction
                {
                    Amount = -outcome.TeacherReversal,
                    TypeId = HardCode.TYPE_TEACHER_TRANSACTION_REFUND,
                    TeacherId = reservation.Slot.TeacherId,
                    ReservationId = reservation.Id,
                }
            );
        }

        if (outcome.TeacherPenalty > 0m)
        {
            context.TeacherWalletTransactions.Add(
                new TeacherWalletTransaction
                {
                    Amount = -outcome.TeacherPenalty,
                    TypeId = HardCode.TYPE_TEACHER_TRANSACTION_PENALTY,
                    TeacherId = reservation.Slot.TeacherId,
                    ReservationId = reservation.Id,
                }
            );
        }

        var allCancelled = order.Reservations.All(r => r.StatusId == HardCode.RESERVATION_CANCELLED);
        order.StatusId = allCancelled
            ? HardCode.STATUS_ORDER_REFUNDED
            : HardCode.STATUS_ORDER_REFUNDED_PARTIALLY;
        order.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
