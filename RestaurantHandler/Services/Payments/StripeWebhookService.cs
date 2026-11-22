using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Stripe;

namespace RestaurantHandler.Services;

public class StripeWebhookService(
    CheckoutWebhookService checkoutWebhook,
    StripeConnectService connect,
    StripeRefundService refunds,
    ILogger<StripeWebhookService> logger
)
{
    /// <summary>
    /// Verifies the Stripe webhook signature and reads the payment status.
    /// </summary>
    public async Task<Response<object>> HandleWebhook(string json, string signature, string secret)
    {
        logger.LogInformation(
            "[Webhook] Step 1 - received raw payload: length={PayloadLength}, hasSignature={HasSignature}, signatureLength={SignatureLength}",
            json?.Length ?? 0,
            !string.IsNullOrEmpty(signature),
            signature?.Length ?? 0
        );

        Event stripeEvent;
        try
        {
            // Diagnostic only: never log the full secret. We expose its length and the
            // "whsec_" prefix so we can tell a missing/misconfigured secret apart from a
            // stripped Stripe-Signature header without leaking the value.
            logger.LogInformation(
                "[Webhook] Step 2 - verifying Stripe signature: secretLoaded={SecretLoaded}, secretLength={SecretLength}, secretHasWhsecPrefix={SecretHasWhsecPrefix}",
                !string.IsNullOrEmpty(secret),
                secret?.Length ?? 0,
                secret?.StartsWith("whsec_") ?? false
            );
            stripeEvent = EventUtility.ConstructEvent(json, signature, secret);
            logger.LogInformation("[Webhook] Step 2 - signature verified successfully.");
        }
        catch (StripeException e)
        {
            // Invalid signature or payload.
            logger.LogWarning(
                e,
                "[Webhook] Step 2 - signature verification FAILED: {Message}",
                e.Message
            );
            return new Response<object>
            {
                Status = 400,
                Message = "Signature du webhook invalide",
                Data = null,
            };
        }

        var eventType = stripeEvent.Type.ToStripeWebhookEvent();
        // Every subsequent log line carries the type + id so concurrent webhook
        // invocations can be told apart when the logs are interleaved.
        var evt = $"{stripeEvent.Type}/{stripeEvent.Id}";
        logger.LogInformation(
            "[Webhook {Event}] Step 3 - event parsed: type={EventType}, id={EventId}, mappedType={MappedType}, connectedAccount={ConnectedAccount}",
            evt,
            stripeEvent.Type,
            stripeEvent.Id,
            eventType,
            // Populated by Stripe only for events on connected accounts (Connect).
            stripeEvent.Account ?? "(platform)"
        );

        switch (eventType)
        {
            case StripeWebhookEventEnum.CheckoutSessionCompleted:
                await checkoutWebhook.HandleCompletedAsync(stripeEvent, evt);
                break;
            case StripeWebhookEventEnum.CheckoutSessionExpired:
                await checkoutWebhook.HandleExpiredAsync(stripeEvent, evt);
                break;
            case StripeWebhookEventEnum.PaymentIntentSucceeded:
                checkoutWebhook.HandlePaymentIntentSucceeded(stripeEvent, evt);
                break;
            case StripeWebhookEventEnum.PaymentIntentPaymentFailed:
                checkoutWebhook.HandlePaymentIntentFailed(stripeEvent, evt);
                break;
            case StripeWebhookEventEnum.AccountUpdated:
                await connect.HandleAccountUpdatedAsync(stripeEvent, evt);
                break;
            case StripeWebhookEventEnum.ChargeRefunded:
                await refunds.HandleChargeRefundedAsync(stripeEvent, evt);
                break;
            default:
                logger.LogInformation(
                    "[Webhook {Event}] Step 4 - unhandled Stripe event: {EventType}.",
                    evt,
                    stripeEvent.Type
                );
                break;
        }

        logger.LogInformation(
            "[Webhook {Event}] Step 5 - webhook processed successfully: type={EventType}, id={EventId}.",
            evt,
            stripeEvent.Type,
            stripeEvent.Id
        );
        return new Response<object>
        {
            Status = 200,
            Message = "Webhook traité avec succès",
            Data = null,
        };
    }
}
