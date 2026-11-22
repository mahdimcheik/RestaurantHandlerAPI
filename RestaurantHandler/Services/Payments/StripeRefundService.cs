using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace RestaurantHandler.Services;

public class StripeRefundService(MainContext context, ILogger<StripeRefundService> logger)
{
    public record RefundRequest(
        // Stripe PaymentIntent id of the original charge (Payment.TransactionRef).
        string PaymentIntentId,
        // Amount to refund in the smallest currency unit (cents).
        long Amount,
        // Réservation remboursée. Recopiée en metadata Stripe pour que le webhook
        // charge.refunded reconnaisse un remboursement émis PAR l'app et ne le
        // comptabilise pas une seconde fois (cf. ReconcileChargeRefundsAsync).
        Guid ReservationId,
        // Derive from stable data (e.g. the reservation id) so replaying a cancel
        // reuses the same key and Stripe never issues a second refund.
        string? IdempotencyKey = null
    );

    /// <summary>
    /// Metadata key stamped on every refund the app issues. Son absence sur un refund Stripe
    /// est ce qui identifie un remboursement émis <b>hors application</b> (dashboard Stripe).
    /// </summary>
    public const string RefundReservationMetadataKey = "bonprof_reservation_id";

    /// <summary>
    /// Refunds the student for a single cancelled reservation by issuing a Stripe
    /// Refund against the original PaymentIntent. Money only — the caller owns the
    /// DB side (status transition, Refund row, wallet entry, order status) so the
    /// whole cancellation commits atomically AFTER the money is confirmed returned.
    /// Idempotent via <see cref="RefundRequest.IdempotencyKey"/>: replaying a cancel
    /// reuses the same refund instead of creating a second one. Returns the Stripe
    /// refund id (re_...) on success, or a non-200 Response so the caller aborts
    /// before recording a phantom refund.
    /// </summary>
    public async Task<Response<string>> RefundReservationAsync(RefundRequest request)
    {
        try
        {
            var refundService = new RefundService();
            var refund = await refundService.CreateAsync(
                new RefundCreateOptions
                {
                    PaymentIntent = request.PaymentIntentId,
                    Amount = request.Amount, // smallest currency unit, e.g. cents
                    // Marque le remboursement comme émis par l'app : le webhook charge.refunded
                    // s'en sert pour ne PAS le re-comptabiliser (il n'enregistre que ce qui vient
                    // du dashboard Stripe). Sans ce marqueur, chaque refund de l'app créerait un
                    // second Refund + une seconde reprise de crédit prof.
                    Metadata = new Dictionary<string, string>
                    {
                        [RefundReservationMetadataKey] = request.ReservationId.ToString(),
                    },
                },
                request.IdempotencyKey is null
                    ? null
                    : new RequestOptions { IdempotencyKey = request.IdempotencyKey }
            );

            return new Response<string>
            {
                Status = 200,
                Message = "Remboursement effectué avec succès",
                Data = refund.Id,
            };
        }
        catch (Exception ex)
        {
            return new Response<string>
            {
                Status = 500,
                Message = $"Erreur lors du remboursement: {ex.Message}",
                Data = null,
            };
        }
    }

    /// <summary><c>charge.refunded</c> : voir <see cref="ReconcileChargeRefundsAsync"/>.</summary>
    public async Task HandleChargeRefundedAsync(Event stripeEvent, string evt)
    {
        logger.LogInformation("[Webhook {Event}] Step 4 - handling ChargeRefunded.", evt);
        var charge = stripeEvent.Data.Object as Charge;
        logger.LogInformation(
            "[Webhook {Event}] Step 4.1 - charge details: chargeId={ChargeId}, paymentIntentId={PaymentIntentId}, amountRefunded={AmountRefunded}.",
            evt,
            charge?.Id,
            charge?.PaymentIntentId,
            charge?.AmountRefunded
        );

        if (charge is not null)
        {
            await ReconcileChargeRefundsAsync(charge, evt);
        }
    }

    /// <summary>
    /// Réconcilie les remboursements Stripe d'une charge avec le ledger local. Ferme le trou
    /// « remboursement émis depuis le dashboard Stripe » : sans Refund local, l'argent sort sans
    /// que les livres le sachent, et <c>PlatformGain</c> (= net − remboursé − part prof) surévalue
    /// durablement le gain de la plateforme.
    /// <para>
    /// Trois cas, dans cet ordre, pour chaque refund Stripe de la charge :
    /// </para>
    /// <list type="number">
    /// <item>son id est déjà sur un <see cref="Refund"/> local → rien à faire (rejeu du webhook) ;</item>
    /// <item>il porte la metadata <see cref="RefundReservationMetadataKey"/> → il vient de l'app.
    /// On ne le comptabilise pas (le flux d'annulation a déjà écrit le Refund et la reprise de
    /// crédit prof), on se contente de <b>rattraper</b> l'id Stripe si le save du cron n'a pas
    /// abouti — c'est la course « Stripe a répondu, SaveChanges a échoué » ;</item>
    /// <item>sinon → remboursement <b>hors application</b>. On l'enregistre : Refund(s) côté acheteur
    /// + ligne(s) wallet négatives côté prof, exactement comme le ferait une annulation in-app.</item>
    /// </list>
    /// <para>
    /// L'idempotence repose sur <see cref="Refund.StripeRefundId"/> : un refund Stripe déjà
    /// enregistré est ignoré au rejeu, donc ce handler peut être rejoué sans rien dupliquer.
    /// </para>
    /// </summary>
    private async Task ReconcileChargeRefundsAsync(Charge charge, string evt)
    {
        if (string.IsNullOrEmpty(charge.PaymentIntentId))
        {
            logger.LogWarning(
                "[Webhook {Event}] Step 4.2 - charge {ChargeId} has no payment intent; cannot map it to an order.",
                evt,
                charge.Id
            );
            return;
        }

        // Le Payment porte le PaymentIntent (TransactionRef) : c'est le seul lien charge -> commande.
        // Slot est chargé car c'est lui qui porte le prof (Slot.TeacherId) de la ligne wallet.
        var payment = await context
            .Payments.Include(p => p.Order!)
            .ThenInclude(o => o.Reservations)
            .ThenInclude(r => r.Slot)
            .FirstOrDefaultAsync(p =>
                p.TransactionRef == charge.PaymentIntentId && p.ArchivedAt == null
            );

        if (payment?.Order is null)
        {
            logger.LogWarning(
                "[Webhook {Event}] Step 4.2 - no order found for payment intent {PaymentIntentId}; refund not reconciled.",
                evt,
                charge.PaymentIntentId
            );
            return;
        }

        var order = payment.Order;

        // On relit la liste des refunds chez Stripe plutôt que charge.Refunds : l'objet de
        // l'event n'embarque que la première page, et une charge peut avoir plusieurs
        // remboursements partiels successifs.
        var stripeRefunds = await new RefundService().ListAsync(
            new RefundListOptions { Charge = charge.Id, Limit = 100 }
        );

        var localRefunds = await context
            .Refunds.Where(r => r.OrderId == order.Id && r.ArchivedAt == null)
            .ToListAsync();

        // Lignes wallet de la commande : servent uniquement à détecter qu'un prof a DÉJÀ été payé
        // sur une réservation qu'on est en train de rembourser (cas qui demande un Transfer Reversal).
        var reservationIds = order.Reservations.Select(r => r.Id).ToList();
        var walletLines = await context
            .TeacherWalletTransactions.Where(w =>
                w.ReservationId != null
                && reservationIds.Contains(w.ReservationId.Value)
                && w.ArchivedAt == null
            )
            .ToListAsync();

        var now = DateTimeOffset.UtcNow;
        var recordedOutOfBand = 0;
        var changed = false;

        foreach (var stripeRefund in stripeRefunds)
        {
            // 1) Déjà enregistré → rejeu du webhook, rien à faire. C'est la garde d'idempotence.
            if (localRefunds.Any(r => r.StripeRefundId == stripeRefund.Id))
                continue;

            // 2) Émis par l'app : le Refund local existe déjà (créé par l'annulation), seul son
            //    StripeRefundId peut manquer si le SaveChanges du cron a échoué après l'appel Stripe.
            if (
                stripeRefund.Metadata is not null
                && stripeRefund.Metadata.TryGetValue(
                    RefundReservationMetadataKey,
                    out var rawReservationId
                )
                && Guid.TryParse(rawReservationId, out var taggedReservationId)
            )
            {
                var pending = localRefunds.FirstOrDefault(r =>
                    r.ReservationId == taggedReservationId && r.StripeRefundId == null
                );

                if (pending is not null)
                {
                    pending.StripeRefundId = stripeRefund.Id;
                    pending.StatusId = HardCode.STATUS_TRANSACTION_PAID;
                    pending.UpdatedAt = now;
                    changed = true;
                    logger.LogInformation(
                        "[Webhook {Event}] Step 4.3 - refund {StripeRefundId} was issued by the app; backfilled local refund {RefundId}.",
                        evt,
                        stripeRefund.Id,
                        pending.Id
                    );
                    continue;
                }

                // Marqueur présent mais aucun Refund local en attente : l'app a émis ce
                // remboursement et l'a déjà soldé sous un autre id, ou la ligne a été purgée.
                // On retombe volontairement sur le chemin « hors app » pour ne pas perdre l'argent
                // de vue — un doublon visible vaut mieux qu'un remboursement invisible.
                logger.LogWarning(
                    "[Webhook {Event}] Step 4.3 - refund {StripeRefundId} carries reservation {ReservationId} but no pending local refund matches; recording it as out-of-band.",
                    evt,
                    stripeRefund.Id,
                    taggedReservationId
                );
            }

            // 3) Hors application (dashboard Stripe) : l'argent est parti, rien ne le dit en base.
            var amount = stripeRefund.Amount / 100m;
            logger.LogWarning(
                "[Webhook {Event}] Step 4.4 - out-of-band refund detected: stripeRefundId={StripeRefundId}, amount={Amount}, order={OrderId}. Recording it in the ledger.",
                evt,
                stripeRefund.Id,
                amount,
                order.Id
            );

            var remaining = amount;

            // Un refund dashboard porte sur la CHARGE, pas sur une réservation : Stripe ne dit pas
            // laquelle. On l'impute donc aux réservations non encore remboursées, dans un ordre
            // stable, en remplissant chacune jusqu'à son montant. Un remboursement total retombe
            // exactement sur « toutes les réservations » ; un partiel est une imputation *inférée*
            // (d'où le warning) — mais le total de la commande, lui, reste juste, et c'est lui qui
            // porte PlatformGain.
            foreach (var reservation in order.Reservations.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id))
            {
                if (remaining <= 0m)
                    break;

                var alreadyRefunded = localRefunds
                    .Where(r => r.ReservationId == reservation.Id)
                    .Sum(r => r.Amount);
                var refundable = reservation.Amount - alreadyRefunded;
                if (refundable <= 0m)
                    continue;

                var slice = Math.Min(refundable, remaining);
                remaining -= slice;

                // Qualifié : Stripe.Refund est aussi dans le scope (using Stripe).
                var refund = new Models.Refund
                {
                    Id = Guid.NewGuid(),
                    Amount = slice,
                    ReservationId = reservation.Id,
                    OrderId = order.Id,
                    PaymentId = payment.Id,
                    StripeRefundId = stripeRefund.Id,
                    StatusId = HardCode.STATUS_TRANSACTION_PAID, // l'argent est déjà parti chez Stripe
                    Reason = "Remboursement émis hors application (dashboard Stripe)",
                    CreatedAt = now,
                };
                context.Refunds.Add(refund);
                localRefunds.Add(refund); // visible pour les refunds suivants de la même boucle

                // Miroir côté vendeur : on reprend la part prof au prorata du montant remboursé.
                // Un remboursement total annule exactement le crédit, comme une annulation in-app.
                var teacherReversal = decimal.Round(
                    slice * (1m - reservation.CommissionRate),
                    2,
                    MidpointRounding.AwayFromZero
                );

                if (teacherReversal > 0m && reservation.Slot is not null)
                {
                    // Le TYPE de la ligne dépend de ce qui est réellement arrivé à l'argent — pas
                    // du statut de la réservation :
                    //  - crédit encore gelé (PayoutId == null) → REFUND : la ligne neutralise le
                    //    crédit, les deux s'annulent, rien n'a quitté la plateforme. Rien à
                    //    récupérer.
                    //  - crédit DÉJÀ versé (PayoutId != null) → CLAWBACK : l'argent est chez le
                    //    prof et il le doit. C'est une dette, à ce titre recouvrée par compensation
                    //    sur ses versements suivants (JobChron.PayoutTeacherBalances). Une ligne
                    //    REFUND ici serait inerte : le cron la lirait comme le miroir d'un crédit
                    //    vivant, alors que ce crédit est parti.
                    var alreadyPaidOut = walletLines.Any(w =>
                        w.ReservationId == reservation.Id && w.PayoutId != null
                    );

                    context.TeacherWalletTransactions.Add(
                        new TeacherWalletTransaction
                        {
                            Id = Guid.NewGuid(),
                            Amount = -teacherReversal,
                            TypeId = alreadyPaidOut
                                ? HardCode.TYPE_TEACHER_TRANSACTION_CLAWBACK
                                : HardCode.TYPE_TEACHER_TRANSACTION_REFUND,
                            TeacherId = reservation.Slot.TeacherId,
                            ReservationId = reservation.Id,
                            CreatedAt = now,
                        }
                    );

                    if (alreadyPaidOut)
                    {
                        // Pas une erreur : la dette est ouverte et se soldera d'elle-même. Reste à
                        // surveiller — un prof qui ne donne plus de cours ne la remboursera jamais
                        // (récupération immédiate = Transfer Reversal sur
                        // TeacherPayout.StripeTransferId, non automatisé).
                        logger.LogWarning(
                            "[Webhook {Event}] Step 4.5 - reservation {ReservationId} was ALREADY PAID OUT to its teacher but has just been refunded ({Amount}). Booked as a CLAWBACK debt: it will be set off against the teacher's next payouts.",
                            evt,
                            reservation.Id,
                            slice
                        );
                    }
                }

                changed = true;
                recordedOutOfBand++;
            }

            if (remaining > 0m)
            {
                // Plus remboursé que la commande ne vaut (ou réservations déjà toutes remboursées).
                logger.LogError(
                    "[Webhook {Event}] Step 4.6 - refund {StripeRefundId} leaves {Remaining} unattributed on order {OrderId}: the refunded total exceeds what the order's reservations are worth.",
                    evt,
                    stripeRefund.Id,
                    remaining,
                    order.Id
                );
            }
        }

        if (!changed)
        {
            logger.LogInformation(
                "[Webhook {Event}] Step 4.7 - nothing to reconcile on order {OrderId}: every Stripe refund is already in the ledger.",
                evt,
                order.Id
            );
            return;
        }

        // Statut commande recalculé depuis le ledger, uniquement si on vient d'enregistrer un
        // remboursement hors app : le flux in-app pose déjà le sien, on ne lui marche pas dessus.
        if (recordedOutOfBand > 0)
        {
            var refundedTotal = localRefunds.Sum(r => r.Amount);
            order.StatusId =
                refundedTotal >= order.TotalAmount
                    ? HardCode.STATUS_ORDER_REFUNDED
                    : HardCode.STATUS_ORDER_REFUNDED_PARTIALLY;
            order.UpdatedAt = now;

            // La réservation, elle, n'est PAS annulée ici : un remboursement dashboard ne dit pas
            // si le cours est annulé ou s'il s'agit d'un geste commercial. Les livres sont justes
            // (la ligne wallet négative annule le crédit, donc le cron de payout ne versera rien
            // de plus pour cette part), mais le créneau reste réservé — à trancher côté admin.
            logger.LogWarning(
                "[Webhook {Event}] Step 4.8 - order {OrderId}: {Count} out-of-band refund line(s) recorded. The reservations were NOT cancelled — cancel them in-app if the lessons are not happening.",
                evt,
                order.Id,
                recordedOutOfBand
            );
        }

        await context.SaveChangesAsync();
        logger.LogInformation(
            "[Webhook {Event}] Step 4.9 - refunds reconciled on order {OrderId}.",
            evt,
            order.Id
        );
    }
}
