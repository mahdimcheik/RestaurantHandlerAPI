using System.ComponentModel.DataAnnotations;
using RestaurantHandler.Domain.Policies;

namespace RestaurantHandler.Models;

// ---------- Panier (aucun prix : le serveur calcule, R2) ----------

public class CartModificationDto
{
    [Required] public ModificationType Type { get; set; }
    [Required] public Guid IngredientId { get; set; }

    /// <summary>Obligatoire pour un remplacement.</summary>
    public Guid? SubstituteIngredientId { get; set; }

    /// <summary>Pour un supplément ; 1 sinon.</summary>
    [Range(1, 20)] public int Quantity { get; set; } = 1;
}

/// <summary>Choix dans un groupe d'options ; une quantité de 2 = double (au plus le maximum par choix du groupe).</summary>
public class CartOptionChoiceDto
{
    [Required] public Guid ChoiceId { get; set; }
    [Range(1, 20)] public int Quantity { get; set; } = 1;
}

public class CartMenuChoiceDto
{
    [Required] public Guid SlotId { get; set; }
    [Required] public Guid ProductId { get; set; }
    public List<CartModificationDto> Modifications { get; set; } = [];

    /// <summary>Choix dans les groupes d'options du produit (pain, sauces…).</summary>
    public List<CartOptionChoiceDto> OptionChoices { get; set; } = [];
}

public class CartLineDto
{
    public Guid? ProductId { get; set; }
    public Guid? MenuId { get; set; }

    [Range(1, CartPolicy.MaxLineQuantity)]
    public int Quantity { get; set; } = 1;

    public List<CartModificationDto> Modifications { get; set; } = [];
    public List<CartMenuChoiceDto> Choices { get; set; } = [];

    /// <summary>Choix dans les groupes d'options d'un produit (pain, sauces, taille) ; vide pour un menu.</summary>
    public List<CartOptionChoiceDto> OptionChoices { get; set; } = [];

    public CartLine ToCartLine() =>
        new(
            ProductId,
            MenuId,
            Quantity,
            Modifications.Select(ToCart).ToList(),
            Choices
                .Select(c => new CartMenuChoice(c.SlotId, c.ProductId, c.Modifications.Select(ToCart).ToList(), c.OptionChoices.Select(ToCart).ToList()))
                .ToList(),
            OptionChoices.Select(ToCart).ToList()
        );

    private static CartModification ToCart(CartModificationDto m) =>
        new(m.Type, m.IngredientId, m.SubstituteIngredientId, m.Quantity);

    private static CartOptionChoice ToCart(CartOptionChoiceDto c) => new(c.ChoiceId, c.Quantity);
}

public class QuoteRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "Le panier est vide.")]
    [MaxLength(CartPolicy.MaxLines)]
    public List<CartLineDto> Lines { get; set; } = [];
}

public class OrderCreate : QuoteRequest, IValidatableObject
{
    /// <summary>Générée par l'appareil pour ce panier ; un rejeu renvoie la commande déjà créée (R10).</summary>
    [Required]
    [StringLength(64, MinimumLength = 8)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Required] public PickupMode PickupMode { get; set; }

    /// <summary>Début du créneau choisi (tel que renvoyé par /restaurant/slots), si PickupMode = Slot.</summary>
    public DateTimeOffset? SlotStart { get; set; }

    [MaxLength(500)] public string? CustomerNote { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PickupMode == PickupMode.Slot && SlotStart is null)
            yield return new("Choisissez un créneau.", [nameof(SlotStart)]);
    }
}

public class QuoteLine
{
    [Required] public int Index { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public int Quantity { get; set; }
    [Required] public int UnitTotalCents { get; set; }
    [Required] public int LineTotalCents { get; set; }
}

public class QuoteResult
{
    [Required] public int TotalCents { get; set; }
    [Required] public List<QuoteLine> Lines { get; set; } = [];
    [Required] public List<VatLineDto> Vat { get; set; } = [];
}

/// <summary>Détail d'un refus de panier, renvoyé dans <c>issues</c> du ProblemDetails (409).</summary>
public class CartIssueDto
{
    /// <summary>Index de la ligne dans la requête ; -1 pour le panier entier.</summary>
    [Required] public int LineIndex { get; set; }
    [Required] public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Corps d'un refus de panier (409) sur <c>POST /orders/quote</c> et <c>POST /orders</c> :
/// ProblemDetails enrichi du détail par ligne.
/// </summary>
public class CartProblemDetails
{
    [Required] public int Status { get; set; }
    public string? Title { get; set; }
    public string? Detail { get; set; }
    public string? Message { get; set; }
    [Required] public List<CartIssueDto> Issues { get; set; } = [];
}

/// <summary>
/// Disponibilité du paiement en ligne avant la validation (US-E05-1) ; seule la carte en ligne
/// est acceptée (ADR-0013). Indication pour l'écran : <c>POST /orders</c> revérifie tout.
/// </summary>
public class CheckoutOptions
{
    [Required] public bool OnlinePaymentEnabled { get; set; }
}

// ---------- Lecture ----------

public class VatLineDto
{
    [Required] public decimal Rate { get; set; }
    [Required] public int GrossCents { get; set; }
    [Required] public int NetCents { get; set; }
    [Required] public int VatCents { get; set; }
}

public class OrderModificationDto
{
    [Required] public ModificationType Type { get; set; }
    [Required] public string Label { get; set; } = string.Empty;
    [Required] public int Quantity { get; set; }
    [Required] public int UnitPriceCents { get; set; }
}

public class OrderItemDto
{
    [Required] public Guid Id { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? MenuId { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public int Quantity { get; set; }
    [Required] public int UnitPriceCents { get; set; }
    [Required] public int LineTotalCents { get; set; }
    [Required] public List<OrderModificationDto> Modifications { get; set; } = [];

    /// <summary>Produits choisis dans les emplacements d'un menu.</summary>
    [Required] public List<OrderItemDto> Components { get; set; } = [];
}

public class OrderHistoryDto
{
    [Required] public OrderStatus Status { get; set; }
    [Required] public DateTimeOffset ChangedAt { get; set; }
    public string? Reason { get; set; }

    /// <summary>Renseigné pour le Manager uniquement.</summary>
    public string? ChangedBy { get; set; }
}

public class OrderPaymentDto
{
    [Required] public Guid Id { get; set; }
    [Required] public PaymentMethod Method { get; set; }
    [Required] public PaymentStatus Status { get; set; }
    [Required] public int AmountCents { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public class OrderSummary
{
    [Required] public Guid Id { get; set; }
    [Required] public int OrderNumber { get; set; }

    /// <summary>Numéro d'appel du jour.</summary>
    [Required] public int DailyNumber { get; set; }
    [Required] public DateOnly ServiceDate { get; set; }
    [Required] public OrderStatus Status { get; set; }
    [Required] public PickupMode PickupMode { get; set; }
    [Required] public DateTimeOffset PickupAt { get; set; }
    [Required] public bool IsPaid { get; set; }
    [Required] public int TotalCents { get; set; }

    /// <summary>Nom du client.</summary>
    public string? CustomerLabel { get; set; }
    [Required] public DateTimeOffset CreatedAt { get; set; }
}

public class OrderDetails : OrderSummary
{
    [Required] public Guid CustomerId { get; set; }
    public string? CustomerNote { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? ReadyAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    [Required] public int PaidCents { get; set; }

    /// <summary>Page de paiement SumUp de la tentative en cours (commande en attente de paiement).</summary>
    public string? PaymentUrl { get; set; }

    /// <summary>Anomalie de paiement à traiter (Manager uniquement).</summary>
    public string? PaymentIssue { get; set; }

    [Required] public List<OrderItemDto> Items { get; set; } = [];
    [Required] public List<VatLineDto> Vat { get; set; } = [];
    [Required] public List<OrderHistoryDto> History { get; set; } = [];
    [Required] public List<OrderPaymentDto> Payments { get; set; } = [];
}

// ---------- Actions ----------

public class ReasonRequest
{
    [Required(ErrorMessage = "Un motif est obligatoire.")]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class OrderFilter
{
    /// <summary>Jour de service (heure de Paris) ; aujourd'hui par défaut.</summary>
    public DateOnly? Date { get; set; }
    public OrderStatus? Status { get; set; }
    public bool? IsPaid { get; set; }

    /// <summary>Numéro d'appel du jour (recherche rapide à la remise).</summary>
    [Range(1, int.MaxValue)]
    public int? Number { get; set; }
}

/// <summary>
/// Justificatif d'une commande payée (US-E05-5). Contenu à valider avec l'expert-comptable
/// (question 2) : ce n'est pas un ticket de caisse au sens fiscal.
/// </summary>
public class OrderReceipt
{
    [Required] public string TradeName { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string? Address { get; set; }
    public string? Siret { get; set; }
    public string? VatNumber { get; set; }
    [Required] public int OrderNumber { get; set; }
    [Required] public int DailyNumber { get; set; }
    [Required] public DateTimeOffset PaidAt { get; set; }
    [Required] public List<OrderItemDto> Items { get; set; } = [];
    [Required] public List<VatLineDto> Vat { get; set; } = [];
    [Required] public int TotalCents { get; set; }
    [Required] public List<OrderPaymentDto> Payments { get; set; } = [];
}
