using RestaurantHandler.Contexts;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace RestaurantHandler.Services;

public class PaymentService(MainContext context, ILogger<PaymentService> logger)
{
    /// <summary>
    /// Backfills the Stripe processing fee and net amount onto a payment. The fee/net live on the
    /// charge's BalanceTransaction, which Stripe attaches a moment after checkout completes, so this
    /// runs as a delayed background job rather than inline in the webhook. Idempotent: a no-op once
    /// the net is already set. Throwing while the balance transaction is not yet available lets
    /// Hangfire retry with backoff until Stripe settles it.
    /// </summary>
    [AutomaticRetry(Attempts = 10, DelaysInSeconds = new[] { 15, 30, 60, 120, 300 })]
    public async Task BackfillStripeFeesAsync(Guid paymentId)
    {
        var payment = await context.Payments.FirstOrDefaultAsync(p => p.Id == paymentId);
        if (payment is null)
        {
            logger.LogWarning(
                "[StripeFees] payment {PaymentId} not found; nothing to backfill.",
                paymentId
            );
            return;
        }

        // Already captured (e.g. a duplicate/retried job) - nothing to do.
        if (payment.NetAmount is not null)
            return;

        if (string.IsNullOrEmpty(payment.TransactionRef))
        {
            logger.LogWarning(
                "[StripeFees] payment {PaymentId} has no payment intent reference; cannot resolve fee/net.",
                paymentId
            );
            return;
        }

        // Resolve the charge behind the payment intent, then fetch it with its balance
        // transaction expanded - the fee/net are reliably attached to the Charge for card payments.
        var intent = await new PaymentIntentService().GetAsync(payment.TransactionRef);
        if (string.IsNullOrEmpty(intent.LatestChargeId))
        {
            // Not settled yet - throw so Hangfire retries this job with backoff.
            throw new InvalidOperationException(
                $"Payment intent {payment.TransactionRef} has no charge yet; will retry."
            );
        }

        var charge = await new ChargeService().GetAsync(
            intent.LatestChargeId,
            new ChargeGetOptions { Expand = ["balance_transaction"] }
        );

        var balanceTx =
            charge.BalanceTransaction
            ?? throw new InvalidOperationException(
                $"Balance transaction not available yet for charge {intent.LatestChargeId}; will retry."
            );

        // Stripe amounts are in the smallest currency unit (cents).
        payment.StripeFee = balanceTx.Fee / 100m;
        payment.NetAmount = balanceTx.Net / 100m;
        payment.BalanceTransactionId = balanceTx.Id;
        await context.SaveChangesAsync();

        logger.LogInformation(
            "[StripeFees] payment {PaymentId} updated: txn={BalanceTxId}, fee={Fee}, net={Net}.",
            paymentId,
            balanceTx.Id,
            payment.StripeFee,
            payment.NetAmount
        );
    }
}
