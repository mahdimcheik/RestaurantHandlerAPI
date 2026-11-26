using System.ComponentModel.DataAnnotations;
using RestaurantHandler.Models.Interfaces;

namespace RestaurantHandler.Models;

/// <summary>Avancement (docs/20-regles-metier/02-machine-etats-commande.md) ; « payée » est à part.</summary>
public enum OrderStatus
{
    PendingPayment,
    Confirmed,
    InPreparation,
    Ready,
    Completed,
    NoShow,
    Cancelled,
}

public enum PickupMode
{
    /// <summary>Dès que possible : heure estimée = validation + délai ASAP.</summary>
    Asap,
    Slot,
}

public enum ModificationType
{
    Remove,
    Replace,
    Supplement,

    /// <summary>Choix dans un groupe d'options du produit (pain, sauce, taille).</summary>
    Choice,
}

/// <summary>Seul le paiement en ligne SumUp existe (ADR-0013).</summary>
public enum PaymentMethod
{
    SumupOnline,
}

public enum PaymentStatus
{
    Pending,
    Successful,
    Failed,
    Cancelled,
    Refunded,
}

public class Order : BaseModel
{
    /// <summary>Référence technique globale, attribuée par la base.</summary>
    public int OrderNumber { get; set; }

    /// <summary>Jour de service (heure de Paris) et numéro d'appel, remis à zéro chaque jour.</summary>
    public DateOnly ServiceDate { get; set; }
    public int DailyNumber { get; set; }

    /// <summary>Clé générée par l'appareil qui valide (R10) ; unique par auteur.</summary>
    [MaxLength(64)]
    public string IdempotencyKey { get; set; } = string.Empty;

    public PickupMode PickupMode { get; set; }

    /// <summary>Heure de retrait prévue : estimée (ASAP) ou début du créneau.</summary>
    public DateTimeOffset PickupAt { get; set; }

    public Guid CustomerId { get; set; }
    public UserApp? Customer { get; set; }

    /// <summary>Le client lui-même.</summary>
    public Guid CreatedByUserId { get; set; }
    public UserApp? CreatedByUser { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.PendingPayment;

    [MaxLength(500)]
    public string? CustomerNote { get; set; }

    /// <summary>Toujours recalculé côté serveur.</summary>
    public int TotalCents { get; set; }

    /// <summary>Renseigné quand les paiements réussis couvrent le total (R9).</summary>
    public DateTimeOffset? PaidAt { get; set; }

    /// <summary>
    /// Anomalie de paiement à traiter par un Manager (montant SumUp ≠ total, remboursement en
    /// échec…) : aucune transition automatique n'a lieu (R9).
    /// </summary>
    [MaxLength(500)]
    public string? PaymentIssue { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? ReadyAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Jeton de concurrence (xmin) : deux gestes simultanés sur une commande → 409.</summary>
    public uint Version { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();
}

/// <summary>
/// Ligne de commande : un produit, un menu, ou (avec <see cref="ParentItemId"/>) le produit
/// choisi dans un emplacement du menu parent. Nom, prix et TVA sont copiés à la commande.
/// </summary>
public class OrderItem : BaseModel
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    public Guid? ParentItemId { get; set; }
    public OrderItem? ParentItem { get; set; }
    public ICollection<OrderItem> Children { get; set; } = new List<OrderItem>();

    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }

    public Guid? MenuId { get; set; }
    public Menu? Menu { get; set; }

    public Guid? MenuSlotId { get; set; }
    public MenuSlot? MenuSlot { get; set; }

    [Required]
    [MaxLength(128)]
    public string NameSnapshot { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;

    /// <summary>Prix du produit/menu, ou supplément du slot pour un composant de menu.</summary>
    public int UnitPriceCents { get; set; }

    public decimal VatRate { get; set; }

    public ICollection<OrderItemModification> Modifications { get; set; } =
        new List<OrderItemModification>();
}

/// <summary>Personnalisation d'une ligne produit (retrait, remplacement, supplément, choix d'option).</summary>
public class OrderItemModification : BaseModel
{
    public Guid OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }

    public ModificationType Type { get; set; }

    /// <summary>Obligatoire sauf pour un choix d'option sans ingrédient (une taille).</summary>
    public Guid? IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    /// <summary>Renseigné si et seulement si <see cref="Type"/> vaut Choice.</summary>
    public Guid? OptionChoiceId { get; set; }
    public OptionChoice? OptionChoice { get; set; }

    /// <summary>Renseigné si et seulement si <see cref="Type"/> vaut Replace.</summary>
    public Guid? SubstituteIngredientId { get; set; }
    public Ingredient? SubstituteIngredient { get; set; }

    /// <summary>« Sans oignons », « Cheddar → Chèvre », « + Bacon », « Sauces : Algérienne ».</summary>
    [Required]
    [MaxLength(128)]
    public string LabelSnapshot { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;
    public int UnitPriceCents { get; set; }

    /// <summary>Ordre d'affichage dans la ligne (choix dans l'ordre de la carte).</summary>
    public int Position { get; set; }
}

/// <summary>Tentative de paiement SumUp ; une commande peut en avoir plusieurs.</summary>
public class Payment : BaseModel
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public int AmountCents { get; set; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "EUR";

    [MaxLength(128)]
    public string? SumupCheckoutId { get; set; }

    [MaxLength(128)]
    public string? SumupTransactionCode { get; set; }

    /// <summary>Identifiant (UUID) de la transaction SumUp, requis pour rembourser.</summary>
    [MaxLength(64)]
    public string? SumupTransactionId { get; set; }

    /// <summary>Page de paiement hébergée par SumUp (valable 30 min).</summary>
    [MaxLength(500)]
    public string? CheckoutUrl { get; set; }

    public DateTimeOffset? RefundedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}

public class OrderStatusHistory
{
    public long Id { get; set; }

    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    public OrderStatus Status { get; set; }

    public Guid? ChangedByUserId { get; set; }
    public UserApp? ChangedByUser { get; set; }

    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Motif d'annulation, affiché au client.</summary>
    [MaxLength(500)]
    public string? Reason { get; set; }
}
