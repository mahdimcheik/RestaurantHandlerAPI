using System.ComponentModel.DataAnnotations;
using RestaurantHandler.Models.Interfaces;

namespace RestaurantHandler.Models;

public enum OrderChannel
{
    Online,

    /// <summary>Saisie au comptoir par un Manager.</summary>
    Counter,
}

public enum OrderStatus
{
    PendingPayment,
    Paid,
    InPreparation,
    Ready,
    Completed,
    Cancelled,
}

public enum ModificationType
{
    Remove,
    Replace,
    Supplement,
}

public enum PaymentMethod
{
    SumupTerminal,
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
    /// <summary>Numéro affiché en cuisine, attribué par la base.</summary>
    public int OrderNumber { get; set; }

    /// <summary>Nul pour un client de passage.</summary>
    public Guid? CustomerId { get; set; }
    public UserApp? Customer { get; set; }

    /// <summary>Le client lui-même, ou le Manager au comptoir.</summary>
    public Guid CreatedByUserId { get; set; }
    public UserApp? CreatedByUser { get; set; }

    public OrderChannel Channel { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.PendingPayment;

    [MaxLength(500)]
    public string? CustomerNote { get; set; }

    /// <summary>Toujours recalculé côté serveur.</summary>
    public int TotalCents { get; set; }

    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? ReadyAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

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

/// <summary>Personnalisation d'une ligne produit (retrait, remplacement, supplément).</summary>
public class OrderItemModification : BaseModel
{
    public Guid OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }

    public ModificationType Type { get; set; }

    public Guid IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    /// <summary>Renseigné si et seulement si <see cref="Type"/> vaut Replace.</summary>
    public Guid? SubstituteIngredientId { get; set; }
    public Ingredient? SubstituteIngredient { get; set; }

    /// <summary>« Sans oignons », « Cheddar → Chèvre », « + Bacon ».</summary>
    [Required]
    [MaxLength(128)]
    public string LabelSnapshot { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;
    public int UnitPriceCents { get; set; }
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
}
