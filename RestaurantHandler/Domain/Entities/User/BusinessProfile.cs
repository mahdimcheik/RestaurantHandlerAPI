using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models;

/// <summary>
/// Informations d'entreprise d'un client professionnel, déclarées par lui (SIREN contrôlé par sa
/// clé). Facultatif : sa présence fait du client un professionnel qui peut demander une facture.
/// </summary>
public class BusinessProfile
{
    [Key]
    public Guid UserId { get; set; }
    public UserApp? User { get; set; }

    [MaxLength(9)]
    public string Siren { get; set; } = string.Empty;

    [MaxLength(14)]
    public string? Siret { get; set; }

    [MaxLength(200)]
    public string LegalName { get; set; } = string.Empty;

    [MaxLength(13)]
    public string? VatNumber { get; set; }

    [MaxLength(200)]
    public string AddressLine1 { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? AddressLine2 { get; set; }

    [MaxLength(10)]
    public string PostalCode { get; set; } = string.Empty;

    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    /// <summary>Code pays ISO 3166-1 alpha-2.</summary>
    [MaxLength(2)]
    public string CountryCode { get; set; } = "FR";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
