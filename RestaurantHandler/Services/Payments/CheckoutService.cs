using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using TerminalApi.Services;

namespace RestaurantHandler.Services;

public class CheckoutService(
    MainContext context,
    JobChron jobChron,
    ILogger<CheckoutService> logger
)
{
    public record CheckoutRequest(Guid OrderId, string Currency = "eur");

    /// <summary>
    /// Creates a Stripe Checkout Session for an order and returns the URL the client should be
    /// redirected to. Each reservation of the order becomes a line item so the buyer sees the
    /// detail of what they are paying for in the Stripe interface.
    /// <para>
    /// Une commande n'a JAMAIS deux liens de paiement vivants en même temps — sinon l'élève peut
    /// être débité deux fois et il faut le rembourser. Trois gardes, dans cet ordre :
    /// </para>
    /// <list type="number">
    /// <item>la commande doit être <c>PENDING</c> (on ne rouvre pas un paiement déjà encaissé) ;</item>
    /// <item>si une session existe et est déjà <c>complete</c>, on refuse : l'élève a payé et le
    /// webhook n'a simplement pas encore basculé la commande. Sinon on expire l'ancienne session
    /// avant d'en émettre une nouvelle (le panier a pu changer) ;</item>
    /// <item>le corps exact de la requête Stripe (génération + expiration) est <b>figé en base
    /// avant</b> l'appel, puis envoyé avec la clé d'idempotence <c>checkout_{orderId}_{attempt}</c>.
    /// Stripe refuse une même clé avec des paramètres différents : figer d'abord est ce qui rend le
    /// rejeu identique si la session part mais que le save local échoue — Stripe renvoie alors la
    /// session déjà créée au lieu d'en ouvrir une seconde. Le <c>ConcurrencyCheck</c> sur
    /// <see cref="Order.CheckoutAttempt"/> fait échouer la seconde de deux requêtes concurrentes
    /// (double-clic) plutôt que de lui laisser ouvrir un lien parallèle.
    /// </item>
    /// </list>
    /// </summary>
    public async Task<Response<object>> CreateCheckoutAsync(
        CheckoutRequest request,
        CallerIdentity? principal
    )
    {
        var user = CheckUser.GetUser(principal, context);
        if (user is null)
        {
            return new Response<object>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
                Data = null,
            };
        }

        if (!user.EmailConfirmed)
        {
            return new Response<object>
            {
                Status = 403,
                Message = AccountActivation.RequiredToBookMessage,
                Data = null,
            };
        }

        // La commande d'un autre élève est traitée comme inexistante : ni l'expirer, ni en
        // émettre une nouvelle session, ni révéler qu'elle existe.
        var order = await context
            .Orders.Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .ThenInclude(r => r.Cursus)
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .ThenInclude(r => r.Slot)
            .FirstOrDefaultAsync(o =>
                o.Id == request.OrderId
                && o.ArchivedAt == null
                && o.Student != null
                && o.Student.UserId == user.Id
            );

        if (order is null)
        {
            return new Response<object>
            {
                Status = 404,
                Message = "Commande non trouvée",
                Data = null,
            };
        }

        if (order.Reservations.Count == 0)
        {
            return new Response<object>
            {
                Status = 400,
                Message = "La commande ne contient aucune réservation",
                Data = null,
            };
        }

        // Garde 0 : prof pas encore pleinement onboardé (Stripe et/ou vérification
        // documentaire) au moment de la commande — aucun paiement ne transite par la
        // plateforme pour elle, le prof valide/refuse manuellement (cf.
        // Order.PaymentRequired). Le frontend ne doit jamais proposer de bouton payer
        // pour une telle commande ; ce garde protège quand même le backend.
        if (!order.PaymentRequired)
        {
            return new Response<object>
            {
                Status = 409,
                Message = "Cette commande ne nécessite pas de paiement en ligne",
                Data = null,
            };
        }

        // Garde 1 : payée (ou annulée) = plus aucun lien de paiement à émettre.
        if (order.StatusId != HardCode.STATUS_ORDER_PENDING)
        {
            return new Response<object>
            {
                Status = 409,
                Message = "Cette commande n'est plus en attente de paiement",
                Data = null,
            };
        }

        // Garde 2 : l'ancienne session. Déjà payée -> on refuse (le webhook va basculer la
        // commande d'un instant à l'autre) ; encore ouverte -> on l'expire, car la nouvelle
        // session reflètera le panier courant et l'ancien lien doit mourir avec lui.
        if (order.CheckoutID is not null)
        {
            try
            {
                var existing = await new SessionService().GetAsync(order.CheckoutID);

                if (existing.Status == "complete" || existing.PaymentStatus == "paid")
                {
                    return new Response<object>
                    {
                        Status = 409,
                        Message = "Cette commande a déjà été payée",
                        Data = null,
                    };
                }

                if (existing.Status == "open")
                {
                    await jobChron.ExpireCheckout(order.CheckoutID);
                }
            }
            catch (StripeException ex)
            {
                // Session inconnue de Stripe (clé rotée, données de test purgées) : rien à
                // expirer, on peut en émettre une nouvelle sans risque de double lien.
                logger.LogWarning(
                    ex,
                    "Session Checkout {CheckoutId} introuvable pour la commande {OrderId}",
                    order.CheckoutID,
                    order.Id
                );
            }
        }

        // Garde 2 bis : délai de paiement dépassé. Après la garde 2, pour qu'une session réglée
        // juste avant l'échéance (webhook pas encore reçu) soit refusée comme payée et non
        // libérée. Émettre un nouveau lien ici relancerait une commande échue pour une fenêtre
        // complète et garderait ses créneaux bloqués.
        if (order.PaymentDeadline is not null && order.PaymentDeadline < DateTimeOffset.UtcNow)
        {
            JobChron.ReleasePendingOrder(context, order, "Délai de paiement dépassé");
            await context.SaveChangesAsync();

            return new Response<object>
            {
                Status = 409,
                Message = "Le délai de paiement de cette commande est dépassé",
                Data = null,
            };
        }

        // Garde 3 : on fige le corps de la requête Stripe AVANT de l'envoyer. L'expiration est
        // arrondie à la minute supérieure — ce qui la rend rejouable à l'identique (sinon elle
        // dépend de l'instant de l'appel et Stripe rejette la clé) et garantit au passage le
        // minimum de 30 min exigé par Stripe, que `now + CHECKOUT_WINDOW` frôlait de trop près.
        var deadline = CeilToMinute(DateTimeOffset.UtcNow.Add(HardCode.CHECKOUT_WINDOW));
        var attempt = order.CheckoutAttempt + 1;

        order.CheckoutAttempt = attempt;
        order.PaymentDeadline = deadline;
        order.CheckoutID = null;

        try
        {
            // Le ConcurrencyCheck sur CheckoutAttempt joue ici : si une requête concurrente a
            // déjà pris la génération, celle-ci échoue au lieu d'ouvrir un second lien.
            await context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return new Response<object>
            {
                Status = 409,
                Message = "Une session de paiement est déjà en cours de création",
                Data = null,
            };
        }

        // One line item per reservation so the buyer sees the detail in the Stripe interface.
        var lineItems = order
            .Reservations.Select(reservation => new SessionLineItemOptions
            {
                Quantity = 1,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = request.Currency,
                    UnitAmount = (long)(reservation.Amount * 100), // montant brut figé (tarif × durée), en plus petite unité (centimes)
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = reservation.Cursus?.Name ?? reservation.Title,
                        Description = reservation.Slot is not null
                            ? $"{reservation.Title} — du {reservation.Slot.DateFrom:dd/MM/yyyy HH:mm} au {reservation.Slot.DateTo:dd/MM/yyyy HH:mm}"
                            : reservation.Title,
                    },
                },
            })
            .ToList();

        // Stripe owns the expiry clock: when this moment is reached it fires a
        // "checkout.session.expired" webhook, which is our precise, restart-proof
        // cleanup trigger (Stripe retries the event for days if we're down).
        // Max allowed by Stripe is 24h; the 30-minute minimum is guaranteed by the
        // rounding above. Read back from the frozen deadline, never recomputed here.
        var options = new SessionCreateOptions
        {
            Mode = "payment",
            ExpiresAt = deadline.UtcDateTime,
            SuccessUrl =
                $"{EnvironmentVariables.API_FRONT_URL}/payment/success?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{EnvironmentVariables.API_FRONT_URL}/payment/cancel",
            LineItems = lineItems,
            // Keeps the link between the Stripe session and the order for the webhook.
            ClientReferenceId = order.Id.ToString(),
            Metadata = new Dictionary<string, string>
            {
                ["orderId"] = order.Id.ToString(),
                ["orderNumber"] = order.OrderNumber,
            },
        };

        // Reflect the order reduction in Stripe through a one-time coupon, if any.
        if (order.ReductionPercentage > 0 || order.ReductionAmount > 0)
        {
            var couponOptions =
                order.ReductionPercentage > 0
                    ? new CouponCreateOptions
                    {
                        PercentOff = order.ReductionPercentage,
                        Duration = "once",
                    }
                    : new CouponCreateOptions
                    {
                        AmountOff = (long)(order.ReductionAmount * 100), // smallest currency unit, e.g. cents
                        Currency = request.Currency,
                        Duration = "once",
                    };

            // Le coupon porte lui aussi une clé dérivée de la génération : sans elle, un rejeu
            // créerait un coupon neuf, donc un corps de requête différent pour la même clé de
            // session — et Stripe rejetterait la session au lieu de la rejouer.
            var coupon = await new CouponService().CreateAsync(
                couponOptions,
                new RequestOptions { IdempotencyKey = $"coupon_{order.Id}_{attempt}" }
            );
            options.Discounts = [new SessionDiscountOptions { Coupon = coupon.Id }];
        }

        var service = new SessionService();
        var session = await service.CreateAsync(
            options,
            // Dérivée de la génération figée ci-dessus : un rejeu (session partie, save perdu)
            // renvoie la MÊME session au lieu d'ouvrir un second lien payable.
            new RequestOptions { IdempotencyKey = $"checkout_{order.Id}_{attempt}" }
        );

        // update order. PaymentDeadline mirrors the Stripe session expiry so the
        // buyer-facing countdown and the server-side cleanup agree exactly.
        order.CheckoutID = session.Id;
        order.PaymentDeadline = session.ExpiresAt;
        await context.SaveChangesAsync();

        return new Response<object>
        {
            Status = 200,
            Message = "Session de paiement créée avec succès",
            Data = new
            {
                id = session.Id,
                url = session.Url,
                publishableKey = EnvironmentVariables.STRIPE_PK,
                // Exact deadline (UTC) the client should count down to.
                expiresAt = session.ExpiresAt,
            },
        };
    }

    /// <summary>
    /// Arrondit à la minute supérieure. Utilisé pour l'expiration d'une session Checkout : la
    /// valeur devient recursusible d'un appel à l'autre (condition pour rejouer une clé
    /// d'idempotence Stripe, qui exige des paramètres identiques) et reste toujours ≥ la fenêtre
    /// demandée, donc au-dessus du minimum de 30 min imposé par Stripe.
    /// </summary>
    private static DateTimeOffset CeilToMinute(DateTimeOffset value)
    {
        var remainder = value.Ticks % TimeSpan.TicksPerMinute;
        return remainder == 0 ? value : value.AddTicks(TimeSpan.TicksPerMinute - remainder);
    }
}
