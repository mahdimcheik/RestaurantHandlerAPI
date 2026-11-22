using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

public class OrderAccountingService(MainContext context)
{
    // Ligne de wallet réduite au strict nécessaire pour la compta, rattachée à sa commande.
    public sealed record WalletLine(
        Guid OrderId,
        Guid ReservationId,
        decimal Amount,
        Guid? PayoutId
    );

    // Lignes wallet de plusieurs commandes en UNE requête (pas de N+1 sur un bilan).
    // TeacherWalletTransaction n'a pas de navigation vers Order : on passe par
    // Reservation.OrderId. Les lignes PAYOUT ont ReservationId null → exclues d'office.
    // Les lignes archivées le sont aussi : le cron de payout archive une ligne de dette
    // qu'il scinde en deux (part recouvrée + reste dû), et la compter en plus de ses deux
    // filles doublerait la dette dans PlatformGain.
    public async Task<ILookup<Guid, WalletLine>> LoadWalletLinesAsync(
        IReadOnlyList<Guid> orderIds
    )
    {
        if (orderIds.Count == 0)
            return Enumerable.Empty<WalletLine>().ToLookup(w => w.OrderId);

        var lines = await context
            .TeacherWalletTransactions.Where(w =>
                w.ArchivedAt == null
                && w.ReservationId != null
                && w.Reservation!.OrderId != null
                && orderIds.Contains(w.Reservation.OrderId.Value)
            )
            .Select(w => new
            {
                OrderId = w.Reservation!.OrderId,
                w.ReservationId,
                w.Amount,
                w.PayoutId,
            })
            .ToListAsync();

        return lines
            .Select(w => new WalletLine(
                w.OrderId!.Value,
                w.ReservationId!.Value,
                w.Amount,
                w.PayoutId
            ))
            .ToLookup(w => w.OrderId);
    }

    // LA formule comptable — source de vérité unique, partagée par l'état comptable
    // d'une commande et par le bilan mensuel. Tout est dérivé du ledger (Payment /
    // TeacherWalletTransaction / Refund) ; aucune valeur n'est stockée.
    // L'appelant fournit les lignes wallet déjà chargées (cf. LoadWalletLinesAsync).
    public static OrderAccounting BuildAccounting(
        Order order,
        IEnumerable<WalletLine> walletLines
    )
    {
        var wallet = walletLines.ToList();
        var reservations = order.Reservations?.ToList() ?? [];

        var teacherOwed = wallet.Where(w => w.PayoutId == null).Sum(w => w.Amount);
        var teacherPaidOut = wallet.Where(w => w.PayoutId != null).Sum(w => w.Amount);
        var teacherTotal = teacherOwed + teacherPaidOut;

        var refundedAmount = order.Refunds?.Sum(r => r.Amount) ?? 0m;
        var refundedReservationIds = order.Refunds?.Select(r => r.ReservationId).ToHashSet() ?? [];

        var payment = order.Payment;
        var isPaid = payment != null;
        var grossPaid = payment?.Amount ?? 0m;
        var stripeFee = payment?.StripeFee;
        // Frais inconnus uniquement si la commande est payée mais pas encore backfillée.
        var stripeFeePending = isPaid && stripeFee == null;
        var netCollected = payment?.NetAmount ?? (grossPaid - (stripeFee ?? 0m));
        var platformGain = netCollected - refundedAmount - teacherTotal;

        var paidOutReservationIds = wallet
            .Where(w => w.PayoutId != null)
            .Select(w => w.ReservationId)
            .ToHashSet();

        var lines = reservations
            .Select(r => new AccountingLine
            {
                ReservationId = r.Id,
                Amount = r.Amount,
                CommissionRate = r.CommissionRate,
                TeacherShare = decimal.Round(
                    r.Amount * (1m - r.CommissionRate),
                    2,
                    MidpointRounding.AwayFromZero
                ),
                Refunded = refundedReservationIds.Contains(r.Id),
                PaidOut = paidOutReservationIds.Contains(r.Id),
            })
            .ToList();

        return new OrderAccounting
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            StatusName = order.Status?.Name,
            IsPaid = isPaid,
            GrossPaid = grossPaid,
            StripeFee = stripeFee,
            StripeFeePending = stripeFeePending,
            NetCollected = netCollected,
            RefundedAmount = refundedAmount,
            TeacherOwed = teacherOwed,
            TeacherPaidOut = teacherPaidOut,
            TeacherTotal = teacherTotal,
            PlatformGain = platformGain,
            PlatformGainProvisional = stripeFeePending,
            Lines = lines,
        };
    }

    // État comptable d'une commande (admin).
    public async Task<Response<OrderAccounting>> GetOrderAccountingAsync(Guid orderId)
    {
        var order = await context
            .Orders.Where(o => o.Id == orderId)
            .Include(o => o.Status)
            .Include(o => o.Payment)
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .Include(o => o.Refunds)
            .FirstOrDefaultAsync();

        if (order is null)
        {
            return new Response<OrderAccounting>
            {
                Status = 404,
                Message = "Commande introuvable",
                Data = null,
            };
        }

        var walletLines = await LoadWalletLinesAsync([order.Id]);

        return new Response<OrderAccounting>
        {
            Status = 200,
            Message = "État comptable récupéré avec succès",
            Data = BuildAccounting(order, walletLines[order.Id]),
        };
    }
}
