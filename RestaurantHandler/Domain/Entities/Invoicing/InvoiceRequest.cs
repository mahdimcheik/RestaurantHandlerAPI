using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models;

public enum InvoiceRequestStatus
{
    /// <summary>Enregistrée, pas encore transmise (aucune plateforme configurée, ou envoi à venir).</summary>
    Pending,
    Submitted,
    Accepted,
    Rejected,

    /// <summary>Envoi en échec : sera retenté.</summary>
    Failed,
}

/// <summary>
/// Demande de facture électronique d'une commande payée par un client professionnel (outbox,
/// ADR-0014). Acheteur et vendeur sont figés à la création (ADR-0008) : une modification
/// ultérieure du profil ou des paramètres du restaurant ne change pas la facture.
/// </summary>
public class InvoiceRequest
{
    [Key]
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    public InvoiceRequestStatus Status { get; set; } = InvoiceRequestStatus.Pending;

    // ---------- Acheteur (snapshot du profil professionnel) ----------
    [MaxLength(9)] public string BuyerSiren { get; set; } = string.Empty;
    [MaxLength(14)] public string? BuyerSiret { get; set; }
    [MaxLength(200)] public string BuyerLegalName { get; set; } = string.Empty;
    [MaxLength(13)] public string? BuyerVatNumber { get; set; }
    [MaxLength(200)] public string BuyerAddressLine1 { get; set; } = string.Empty;
    [MaxLength(200)] public string? BuyerAddressLine2 { get; set; }
    [MaxLength(10)] public string BuyerPostalCode { get; set; } = string.Empty;
    [MaxLength(100)] public string BuyerCity { get; set; } = string.Empty;
    [MaxLength(2)] public string BuyerCountryCode { get; set; } = "FR";

    // ---------- Vendeur (snapshot des paramètres du restaurant) ----------
    [MaxLength(200)] public string SellerLegalName { get; set; } = string.Empty;
    [MaxLength(14)] public string? SellerSiret { get; set; }
    [MaxLength(13)] public string? SellerVatNumber { get; set; }
    [MaxLength(300)] public string? SellerAddress { get; set; }

    // ---------- Transmission ----------
    /// <summary>Plateforme agréée qui a reçu la demande.</summary>
    [MaxLength(64)] public string? Provider { get; set; }

    /// <summary>Identifiant de la facture chez la plateforme.</summary>
    [MaxLength(128)] public string? ExternalId { get; set; }

    [MaxLength(1000)] public string? LastError { get; set; }
    public int Attempts { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
