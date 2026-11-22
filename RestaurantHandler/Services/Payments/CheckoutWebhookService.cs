using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using TerminalApi.Services;

namespace RestaurantHandler.Services;

public class CheckoutWebhookService(MainContext context, ILogger<CheckoutWebhookService> logger)
{
    /// <summary>
    /// <c>checkout.session.completed</c> : la transition PENDING → PAID est la garde
    /// anti-doublon.
    /// </summary>
    public async Task HandleCompletedAsync(Event stripeEvent, string evt)
    {
        logger.LogInformation(
            "[Webhook {Event}] Step 4 - handling CheckoutSessionCompleted.",
            evt
        );
        var session = stripeEvent.Data.Object as Session;
        logger.LogInformation(
            "[Webhook {Event}] Step 4.1 - checkout session details: sessionId={SessionId}, paymentStatus={PaymentStatus}, paymentIntentId={PaymentIntentId}",
            evt,
            session?.Id,
            session?.PaymentStatus,
            session?.PaymentIntentId
        );

        // Only mark the order paid once Stripe confirms the funds were captured.
        if (
            session?.PaymentStatus == "paid"
            && session.Metadata is not null
            && session.Metadata.TryGetValue("orderId", out var orderIdValue)
            && Guid.TryParse(orderIdValue, out var orderId)
        )
        {
            logger.LogInformation(
                "[Webhook {Event}] Step 4.2 - payment confirmed and orderId resolved: orderId={OrderId}. Loading order from database.",
                evt,
                orderId
            );
            var order = await context
                .Orders.Where(o => o.Id == orderId && o.ArchivedAt == null)
                .Include(o => o.Reservations)
                .ThenInclude(r => r.Cursus)
                .Include(o => o.Reservations)
                .ThenInclude(r => r.Slot)
                .FirstOrDefaultAsync();

            if (order is null)
            {
                logger.LogWarning(
                    "[Webhook {Event}] Step 4.3 - order {OrderId} not found, nothing to update.",
                    evt,
                    orderId
                );
            }
            else if (!order.PaymentRequired)
            {
                // Ne devrait jamais arriver : une commande PaymentRequired == false
                // n'obtient jamais de CheckoutID/session Stripe (cf. BookSlotAsync /
                // CreateCheckoutAsync). Insurance bon marché contre un webhook rejoué
                // ou forgé référençant une commande qui n'a jamais dû passer par Stripe.
                logger.LogWarning(
                    "[Webhook {Event}] Step 4.3 - order {OrderId} does not require payment, ignoring.",
                    evt,
                    orderId
                );
            }
            else if (order.StatusId != HardCode.STATUS_ORDER_PAID)
            {
                logger.LogInformation(
                    "[Webhook {Event}] Step 4.3 - order {OrderId} loaded (currentStatus={CurrentStatus}, reservations={ReservationCount}). Creating payment record.",
                    evt,
                    orderId,
                    order.StatusId,
                    order.Reservations.Count
                );
                var payment = new Payment
                {
                    Amount = order.TotalAmount,
                    OrderId = order.Id,
                    MethodId = HardCode.PAYMENT_METHOD_CARD,
                    TransactionRef = session.PaymentIntentId,
                };

                // The Stripe fee / net live on the charge's BalanceTransaction, which Stripe
                // attaches a moment AFTER the checkout completes - fetching it inline here
                // races Stripe and reliably comes back null. So we save the payment now with
                // fee/net left null and backfill them from a delayed background job below.
                context.Payments.Add(payment);
                await context.SaveChangesAsync();
                logger.LogInformation(
                    "[Webhook {Event}] Step 4.4 - payment record saved: paymentId={PaymentId}, amount={Amount}, transactionRef={TransactionRef}.",
                    evt,
                    payment.Id,
                    payment.Amount,
                    payment.TransactionRef
                );

                // Backfill the Stripe fee / net once the balance transaction has settled.
                // Delayed so Stripe has attached it; Hangfire retries with backoff if it is
                // still not ready (see BackfillStripeFeesAsync).
                BackgroundJob.Schedule<PaymentService>(
                    s => s.BackfillStripeFeesAsync(payment.Id),
                    TimeSpan.FromSeconds(15)
                );

                order.StatusId = HardCode.STATUS_ORDER_PAID;
                // Link is already established by payment.OrderId above (1:1, FK on Payment).
                logger.LogInformation(
                    "[Webhook {Event}] Step 4.5 - updating {ReservationCount} reservation(s) to accepted for order {OrderId}.",
                    evt,
                    order.Reservations.Count,
                    orderId
                );
                foreach (var reservation in order.Reservations)
                {
                    // Action système (webhook) : pas d'acteur utilisateur.
                    reservation.TransitionStatusTo(
                        context,
                        HardCode.RESERVATION_ACCEPTED,
                        reason: "Paiement confirmé"
                    );

                    // Le code de présence n'a de sens que pour un cours payé, seul
                    // contestable : il naît avec le paiement (ADR-0012).
                    reservation.ValidationCode ??= HelpersFunctions.GenerateValidationCode();

                        // Credit the teacher's wallet now that the funds are captured. The
                        // entry stays in the balance (PayoutId == null) until the lesson is
                        // delivered and the scheduled payout job settles it. Reaching this
                        // branch only happens on the first PENDING->PAID transition, so the
                        // order status itself guards against duplicate credits on webhook retries.
                        // On lit le snapshot figé à la réservation (montant brut + taux de
                        // commission) plutôt que de recalculer depuis Cursus, dont le prix
                        // a pu changer entre la réservation et le paiement.
                        var teacherShare = decimal.Round(
                        reservation.Amount * (1m - reservation.CommissionRate),
                        2,
                        MidpointRounding.AwayFromZero
                    );

                    if (reservation.Slot is not null && teacherShare > 0m)
                    {
                        context.TeacherWalletTransactions.Add(
                            new TeacherWalletTransaction
                            {
                                Id = Guid.NewGuid(),
                                Amount = teacherShare,
                                TypeId = HardCode.TYPE_TEACHER_TRANSACTION_PAYMENT,
                                TeacherId = reservation.Slot.TeacherId,
                                ReservationId = reservation.Id,
                                CreatedAt = DateTimeOffset.UtcNow,
                            }
                        );
                    }
                }
                await context.SaveChangesAsync();
                logger.LogInformation(
                    "[Webhook {Event}] Step 4.6 - order {OrderId} marked as paid: paymentId={PaymentId}, amount={Amount}, transactionRef={TransactionRef}, reservations={ReservationCount}.",
                    evt,
                    orderId,
                    payment.Id,
                    payment.Amount,
                    payment.TransactionRef,
                    order.Reservations.Count
                );
            }
            else
            {
                logger.LogInformation(
                    "[Webhook {Event}] Step 4.3 - order {OrderId} already paid, skipping update.",
                    evt,
                    orderId
                );
            }

            // Paid orders are never swept (the sweep only touches pending orders),
            // and a completed session won't emit an "expired" event, so there is no
            // scheduled cleanup to cancel here anymore.
        }
        else
        {
            logger.LogWarning(
                "[Webhook {Event}] Step 4.2 - checkout session {SessionId} ignored: paymentStatus={PaymentStatus}, hasOrderId={HasOrderId}.",
                evt,
                session?.Id,
                session?.PaymentStatus,
                session?.Metadata is not null && session.Metadata.ContainsKey("orderId")
            );
        }
    }

    /// <summary><c>checkout.session.expired</c> : libère la commande si c'est sa session courante.</summary>
    public async Task HandleExpiredAsync(Event stripeEvent, string evt)
    {
        logger.LogInformation(
            "[Webhook {Event}] Step 4 - handling CheckoutSessionExpired.",
            evt
        );
        var session = stripeEvent.Data.Object as Session;
        logger.LogInformation(
            "[Webhook {Event}] Step 4.1 - expired checkout session: sessionId={SessionId}.",
            evt,
            session?.Id
        );

        if (
            session?.Metadata is not null
            && session.Metadata.TryGetValue("orderId", out var expiredOrderIdValue)
            && Guid.TryParse(expiredOrderIdValue, out var expiredOrderId)
        )
        {
            var order = await context
                .Orders.Where(o => o.Id == expiredOrderId && o.ArchivedAt == null)
                .Include(o => o.Reservations)
                .FirstOrDefaultAsync();

            // Only release a still-pending order whose CURRENT checkout is the one
            // that expired. Expiring a session via the API also fires this event, so
            // when we supersede a checkout (new session created, or session dropped
            // because the order changed) the order's CheckoutID no longer matches the
            // stale session's id - skipping here prevents us from wiping a still-active
            // order. A paid order never receives an "expired" event, but the status
            // check also guards against any out-of-order delivery.
            if (
                order is not null
                && order.StatusId == HardCode.STATUS_ORDER_PENDING
                && order.CheckoutID == session.Id
            )
            {
                logger.LogInformation(
                    "[Webhook {Event}] Step 4.2 - releasing expired order {OrderId}: cancelling {ReservationCount} reservation(s) and resetting.",
                    evt,
                    order.Id,
                    order.Reservations.Count
                );
                // Cancelling the pending reservations frees the held slots.
                JobChron.ReleasePendingOrder(context, order, "Session de paiement expirée");
                await context.SaveChangesAsync();
            }
            else
            {
                logger.LogInformation(
                    "[Webhook {Event}] Step 4.2 - expired session {SessionId} ignored for order {OrderId}: not pending, not found, or superseded by a newer checkout.",
                    evt,
                    session?.Id,
                    expiredOrderId
                );
            }
        }
        else
        {
            logger.LogWarning(
                "[Webhook {Event}] Step 4.2 - expired checkout session {SessionId} has no orderId metadata, ignoring.",
                evt,
                session?.Id
            );
        }
    }

    /// <summary><c>payment_intent.succeeded</c> : journalisé seulement.</summary>
    public void HandlePaymentIntentSucceeded(Event stripeEvent, string evt)
    {
        logger.LogInformation(
            "[Webhook {Event}] Step 4 - handling PaymentIntentSucceeded.",
            evt
        );
        var intent = stripeEvent.Data.Object as PaymentIntent;
        logger.LogInformation(
            "[Webhook {Event}] Step 4.1 - payment succeeded: paymentIntentId={PaymentIntentId}, amount={Amount}.",
            evt,
            intent?.Id,
            intent?.Amount
        );
    }

    /// <summary><c>payment_intent.payment_failed</c> : journalisé seulement.</summary>
    public void HandlePaymentIntentFailed(Event stripeEvent, string evt)
    {
        logger.LogInformation(
            "[Webhook {Event}] Step 4 - handling PaymentIntentPaymentFailed.",
            evt
        );
        var intent = stripeEvent.Data.Object as PaymentIntent;
        logger.LogWarning(
            "[Webhook {Event}] Step 4.1 - payment failed: paymentIntentId={PaymentIntentId}, amount={Amount}.",
            evt,
            intent?.Id,
            intent?.Amount
        );
    }
}
