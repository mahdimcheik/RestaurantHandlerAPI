using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models;

/// <summary>Profil professionnel du client connecté.</summary>
public class BusinessProfileDetails
{
    [Required] public string Siren { get; set; } = string.Empty;
    public string? Siret { get; set; }
    [Required] public string LegalName { get; set; } = string.Empty;
    public string? VatNumber { get; set; }
    [Required] public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    [Required] public string PostalCode { get; set; } = string.Empty;
    [Required] public string City { get; set; } = string.Empty;
    [Required] public string CountryCode { get; set; } = "FR";
    [Required] public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public static BusinessProfileDetails From(BusinessProfile p) =>
        new()
        {
            Siren = p.Siren,
            Siret = p.Siret,
            LegalName = p.LegalName,
            VatNumber = p.VatNumber,
            AddressLine1 = p.AddressLine1,
            AddressLine2 = p.AddressLine2,
            PostalCode = p.PostalCode,
            City = p.City,
            CountryCode = p.CountryCode,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
        };
}

/// <summary>
/// Devenir professionnel ou modifier son profil. Espaces tolérés dans les numéros ; le n° de TVA
/// est calculé depuis le SIREN s'il est omis.
/// </summary>
public class BusinessProfileUpsert
{
    [Required(ErrorMessage = "Le SIREN est obligatoire.")]
    [MaxLength(20)]
    public string Siren { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Siret { get; set; }

    [Required(ErrorMessage = "La raison sociale est obligatoire.")]
    [MaxLength(200)]
    public string LegalName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? VatNumber { get; set; }

    [Required(ErrorMessage = "L'adresse est obligatoire.")]
    [MaxLength(200)]
    public string AddressLine1 { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? AddressLine2 { get; set; }

    [Required(ErrorMessage = "Le code postal est obligatoire.")]
    [RegularExpression(@"^\d{5}$", ErrorMessage = "Le code postal doit comporter 5 chiffres.")]
    public string PostalCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "La ville est obligatoire.")]
    [MaxLength(100)]
    public string City { get; set; } = string.Empty;
}

/// <summary>Suivi d'une demande de facture électronique.</summary>
public class InvoiceRequestDetails
{
    [Required] public Guid Id { get; set; }
    [Required] public Guid OrderId { get; set; }
    [Required] public int OrderNumber { get; set; }
    [Required] public InvoiceRequestStatus Status { get; set; }
    [Required] public string BuyerSiren { get; set; } = string.Empty;
    [Required] public string BuyerLegalName { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? ExternalId { get; set; }

    /// <summary>Dernière erreur de transmission (Manager uniquement).</summary>
    public string? LastError { get; set; }
    [Required] public int Attempts { get; set; }
    [Required] public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }

    public static InvoiceRequestDetails From(InvoiceRequest r, int orderNumber, bool forManager) =>
        new()
        {
            Id = r.Id,
            OrderId = r.OrderId,
            OrderNumber = orderNumber,
            Status = r.Status,
            BuyerSiren = r.BuyerSiren,
            BuyerLegalName = r.BuyerLegalName,
            Provider = r.Provider,
            ExternalId = r.ExternalId,
            LastError = forManager ? r.LastError : null,
            Attempts = r.Attempts,
            CreatedAt = r.CreatedAt,
            SubmittedAt = r.SubmittedAt,
        };
}

public class InvoiceRequestFilter
{
    public InvoiceRequestStatus? Status { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
