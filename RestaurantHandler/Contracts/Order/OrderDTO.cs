using RestaurantHandler.Models.Interfaces;
using Stripe;
using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models;

public class StatusOrderDetails
{
    [Required]
    public Guid Id { get; set; }
    [Required]
    public string Name { get; set; } = string.Empty;
    [Required]
    public string Color { get; set; } = string.Empty;
    public string? Icon { get; set; }
    [Required]
    public Dictionary<string, string> Translations { get; set; } = new();

    public StatusOrderDetails() { }

    public StatusOrderDetails(StatusOrder status)
    {
        Id = status.Id;
        Name = status.Name;
        Color = status.Color;
        Icon = status.Icon;
        Translations = status.Translations;
    }
}

public class StatusTransactionDetails
{
    [Required]
    public Guid Id { get; set; }
    [Required]
    public string Name { get; set; } = string.Empty;
    [Required]
    public string Color { get; set; } = string.Empty;
    public string? Icon { get; set; }
    [Required]
    public Dictionary<string, string> Translations { get; set; } = new();

    public StatusTransactionDetails() { }

    public StatusTransactionDetails(StatusTransaction status)
    {
        Id = status.Id;
        Name = status.Name;
        Color = status.Color;
        Icon = status.Icon;
        Translations = status.Translations;
    }
}

public class RefundDetails
{
    [Required]
    public Guid Id { get; set; }

    /// <summary>Montant brut remboursé à l'élève pour la réservation annulée.</summary>
    [Required]
    public decimal Amount { get; set; }

    /// <summary>Réservation annulée à l'origine du remboursement.</summary>
    [Required]
    public Guid ReservationId { get; set; }

    public string? Reason { get; set; }

    /// <summary>Identifiant Stripe du remboursement (re_...), pour réconciliation.</summary>
    public string? StripeRefundId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public StatusTransactionDetails? Status { get; set; }

    public RefundDetails() { }

    public RefundDetails(Refund refund)
    {
        Id = refund.Id;
        Amount = refund.Amount;
        ReservationId = refund.ReservationId;
        Reason = refund.Reason;
        StripeRefundId = refund.StripeRefundId;
        CreatedAt = refund.CreatedAt;

        if (refund.Status != null)
        {
            Status = new StatusTransactionDetails(refund.Status);
        }
    }
}

public class OrderDetails : IUpdateable
{
    [Required]
    public Guid Id { get; set; }
    [Required]
    public string OrderNumber { get; set; } = string.Empty;
    [Required]
    public DateTimeOffset OrderDate { get; set; }

    /// <summary>
    /// Montant initialement commandé et facturé. IMMUABLE après paiement : une
    /// annulation ne le retouche jamais (cf. <see cref="Refunds"/>).
    /// </summary>
    [Required]
    public decimal TotalAmount { get; set; }
    public decimal ReductionPercentage { get; set; }
    public decimal ReductionAmount { get; set; }
    public StatusOrderDetails? Status { get; set; }
    public StudentDetails? Student { get; set; }
    public TeacherDetails? Teacher { get; set; }
    public List<ReservationDetails> Reservations { get; set; } = [];

    /// <summary>Remboursements émis sur la commande (un par réservation annulée).</summary>
    public List<RefundDetails> Refunds { get; set; } = [];

    /// <summary>Total remboursé à l'élève = somme des <see cref="Refunds"/>.</summary>
    public decimal RefundedAmount { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? PaymentDeadline { get; set; }

    /// <summary>
    /// Faux pour une commande dont le prof n'était pas pleinement onboardé (Stripe et/ou
    /// vérification documentaire) au moment de la réservation : aucun paiement en ligne,
    /// le prof valide/refuse manuellement chaque réservation. Figé à la création de la
    /// commande, cf. <see cref="Order.PaymentRequired"/>.
    /// </summary>
    [Required]
    public bool PaymentRequired { get; set; }

    public OrderDetails() { }

    public OrderDetails(Order order)
    {
        Id = order.Id;
        OrderNumber = order.OrderNumber;
        OrderDate = order.OrderDate;
        TotalAmount = order.TotalAmount;
        ReductionPercentage = order.ReductionPercentage;
        ReductionAmount = order.ReductionAmount;
        UpdatedAt = order.UpdatedAt;
        PaymentDeadline = order.PaymentDeadline;
        PaymentRequired = order.PaymentRequired;

        if (order.Status != null)
            Status = new StatusOrderDetails(order.Status);

        if (order.Student != null)
            Student = new StudentDetails(order.Student);

        if (order.Teacher != null)
            Teacher = new TeacherDetails(order.Teacher, includeEmail: true);

        if (order.Reservations != null)
            Reservations = order.Reservations.Select(r => new ReservationDetails(r)).ToList();

        if (order.Refunds != null)
        {
            Refunds = order.Refunds.Select(r => new RefundDetails(r)).ToList();
            RefundedAmount = order.Refunds.Sum(r => r.Amount);
        }
    }
}
