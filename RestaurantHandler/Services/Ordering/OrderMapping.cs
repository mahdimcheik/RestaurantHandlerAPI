using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Models;

namespace RestaurantHandler.Services;

public static class OrderMapping
{
    public static List<VatLineDto> Vat(IEnumerable<OrderItem> topLevel) =>
        VatPolicy.Breakdown(topLevel)
            .Select(v => new VatLineDto { Rate = v.Rate, GrossCents = v.GrossCents, NetCents = v.NetCents, VatCents = v.VatCents })
            .ToList();

    public static int PaidCents(Order order) =>
        order.Payments.Where(p => p.Status == PaymentStatus.Successful).Sum(p => p.AmountCents);

    public static string? CustomerLabel(Order order) =>
        order.Customer is { } c ? $"{c.FirstName} {c.LastName}".Trim() : null;

    public static T FillSummary<T>(T dto, Order o) where T : OrderSummary
    {
        dto.Id = o.Id;
        dto.OrderNumber = o.OrderNumber;
        dto.DailyNumber = o.DailyNumber;
        dto.ServiceDate = o.ServiceDate;
        dto.Status = o.Status;
        dto.PickupMode = o.PickupMode;
        dto.PickupAt = o.PickupAt;
        dto.IsPaid = o.PaidAt is not null;
        dto.TotalCents = o.TotalCents;
        dto.CustomerLabel = CustomerLabel(o);
        dto.CreatedAt = o.CreatedAt;
        return dto;
    }

    public static OrderSummary ToSummary(Order o) => FillSummary(new OrderSummary(), o);

    /// <summary>Attend Items (à plat, avec Modifications), Payments et StatusHistory chargés.</summary>
    public static OrderDetails ToDetails(Order o, bool forManager)
    {
        var topLevel = o.Items.Where(i => i.ParentItemId is null).OrderBy(i => i.CreatedAt).ToList();
        var details = FillSummary(new OrderDetails(), o);
        details.CustomerId = o.CustomerId;
        details.CustomerNote = o.CustomerNote;
        details.ConfirmedAt = o.ConfirmedAt;
        details.ReadyAt = o.ReadyAt;
        details.CompletedAt = o.CompletedAt;
        details.PaidAt = o.PaidAt;
        details.PaidCents = PaidCents(o);
        details.PaymentUrl = o.Status == OrderStatus.PendingPayment
            ? o.Payments.Where(p => p.Status == PaymentStatus.Pending && p.CheckoutUrl != null)
                .OrderByDescending(p => p.CreatedAt).Select(p => p.CheckoutUrl).FirstOrDefault()
            : null;
        details.PaymentIssue = forManager ? o.PaymentIssue : null;
        details.InvoiceRequested = o.InvoiceRequested;
        details.Items = topLevel.Select(ToItem).ToList();
        details.Vat = Vat(topLevel);
        details.History = o.StatusHistory
            .OrderBy(h => h.ChangedAt).ThenBy(h => h.Id)
            .Select(h => new OrderHistoryDto
            {
                Status = h.Status,
                ChangedAt = h.ChangedAt,
                Reason = h.Reason,
                ChangedBy = forManager && h.ChangedByUser is { } u ? $"{u.FirstName} {u.LastName}".Trim() : null,
            })
            .ToList();
        details.Payments = o.Payments
            .OrderBy(p => p.CreatedAt)
            .Select(p => new OrderPaymentDto
            {
                Id = p.Id,
                Method = p.Method,
                Status = p.Status,
                AmountCents = p.AmountCents,
                CompletedAt = p.CompletedAt,
                RefundedAt = p.RefundedAt,
            })
            .ToList();
        return details;
    }

    private static OrderItemDto ToItem(OrderItem i) =>
        new()
        {
            Id = i.Id,
            ProductId = i.ProductId,
            MenuId = i.MenuId,
            Name = i.NameSnapshot,
            Quantity = i.Quantity,
            UnitPriceCents = i.UnitPriceCents,
            LineTotalCents = OrderPricing.LineTotal(i),
            Modifications = i.Modifications
                .OrderBy(m => m.Type).ThenBy(m => m.Position)
                .Select(m => new OrderModificationDto
                {
                    Type = m.Type,
                    Label = m.LabelSnapshot,
                    Quantity = m.Quantity,
                    UnitPriceCents = m.UnitPriceCents,
                })
                .ToList(),
            Note = i.Note,
            Components = i.Children.OrderBy(c => c.MenuSlot?.Position ?? 0).Select(ToItem).ToList(),
        };
}
