using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;
using TerminalApi.Services;

namespace RestaurantHandler.Services;

/// <summary>
/// Le panier d'un élève chez un professeur : la commande PENDING qu'on alimente à la réservation
/// et dont on retire une réservation non payée.
/// </summary>
public class ReservationCartService(MainContext context, JobChron jobChron)
{
    // Orders are teacher-scoped: a student has at most one PENDING order per teacher and
    // per payment mode. Returns that order (the live cart) or creates a new one.
    // Order.PaymentRequired is a frozen snapshot, never re-derived from the teacher's
    // current state, so a cart is only reused when its mode matches: once the teacher
    // finishes onboarding (Stripe…), a new booking must not land in the old no-payment
    // order, which would hide the pay button. The old order stays as is and lives on
    // until the teacher has decided its remaining reservations.
    public async Task<Models.Order> GetActiveOrder(
        Guid studentId,
        Guid teacherId,
        bool paymentRequired
    )
    {
        var activeOrder = await context
            .Orders.Include(o => o.Reservations)
            .Where(o =>
                o.StudentId == studentId
                && o.TeacherId == teacherId
                && o.ArchivedAt == null
                && o.StatusId == HardCode.STATUS_ORDER_PENDING
                && o.PaymentRequired == paymentRequired
            )
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        // Une commande dont le délai de paiement est dépassé n'est plus un panier : la
        // réutiliser reporterait l'échéance (BookingService) et relancerait ses anciennes
        // réservations. On la libère comme le ferait CleanStalePendingOrders.
        if (
            activeOrder is not null
            && activeOrder.PaymentDeadline is not null
            && activeOrder.PaymentDeadline < DateTimeOffset.UtcNow
        )
        {
            if (activeOrder.CheckoutID is not null)
            {
                await jobChron.ExpireCheckout(activeOrder.CheckoutID);
            }

            JobChron.ReleasePendingOrder(context, activeOrder, "Délai de paiement dépassé");
            await context.SaveChangesAsync();
        }

        if (activeOrder is not null)
        {
            return activeOrder;
        }

        var orderId = Guid.NewGuid();
        var newOrder = new Models.Order
        {
            Id = orderId,
            StudentId = studentId,
            TeacherId = teacherId,
            OrderDate = DateTime.UtcNow,
            TotalAmount = 0m,
            CreatedAt = DateTimeOffset.UtcNow,
            PaymentRequired = paymentRequired,
        };

        context.Orders.Add(newOrder);

        // OrderNumber = BP_yyyyMMdd_NNN, NNN = rang dans la journée (UTC). On compte
        // les commandes déjà créées aujourd'hui pour le point de départ, mais c'est
        // l'index unique sur OrderNumber qui garantit l'unicité : deux commandes créées
        // au même instant calculent le même rang, l'une réussit, l'autre attrape la
        // violation 23505 et réessaie avec le rang suivant jusqu'à trouver un libre.
        var dayStart = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);
        var countToday = await context.Orders.CountAsync(o =>
            o.CreatedAt >= dayStart && o.CreatedAt < dayEnd
        );

        for (var attempt = 0; ; attempt++)
        {
            newOrder.OrderNumber = HelpersFunctions.GenerateOrderNumber(
                dayStart.UtcDateTime,
                countToday + 1 + attempt
            );
            try
            {
                await context.SaveChangesAsync();
                return newOrder;
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is Npgsql.PostgresException pg
                    && pg.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation
                    && attempt < 1000
                )
            {
                // Collision concurrente sur OrderNumber : on bump le rang et on retente.
                // EF garde l'entité en Added, il suffit de changer la valeur en conflit.
            }
        }
    }

    // Recompute a PENDING order's total after one of its reservations is removed, and
    // archive the order if that was its last line. Shared by both the student and
    // teacher "remove a not-yet-paid reservation" paths — a cancelled/rejected order
    // never has a Payment, so recomputing TotalAmount here is safe (an order with a
    // Payment is immutable and never reaches this helper).
    private static void ShrinkPendingOrder(Order order, Reservation removed, DateTimeOffset now)
    {
        var newTotal = order.TotalAmount - removed.Amount;
        order.TotalAmount = newTotal >= 0 ? newTotal : 0;
        order.UpdatedAt = now;
        order.OrderDate = now;

        if (order.Reservations.All(r => r.Id == removed.Id))
        {
            order.ArchivedAt = now;
            order.PaymentDeadline = null;
        }
    }

    // Removing a reservation changes the order, so any Stripe checkout already open for
    // it is now stale (its line items no longer match Order.TotalAmount). Best effort:
    // the session may already be expired or completed.
    public async Task ExpireOpenCheckoutAsync(Order order)
    {
        if (order.CheckoutID is null)
        {
            return;
        }

        try
        {
            await jobChron.ExpireCheckout(order.CheckoutID);
            order.CheckoutID = null;
            await context.SaveChangesAsync();
        }
        catch { }
    }

    // A reservation leaving the cart without being paid: transition (row and history
    // kept), order total adjusted, and detached from the order — checkout loads
    // Order.Reservations without filtering on status and would otherwise bill it.
    public void WithdrawFromOrder(
        Reservation reservation,
        Order? order,
        Guid newStatusId,
        Guid actorId,
        string reason,
        DateTimeOffset now
    )
    {
        if (order != null)
        {
            ShrinkPendingOrder(order, reservation, now);
        }

        reservation.TransitionStatusTo(context, newStatusId, actorId, reason);
        reservation.TeacherResponseDeadline = null;
        reservation.Order = null;
        reservation.OrderId = null;
    }
}
