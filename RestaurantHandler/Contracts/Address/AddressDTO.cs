using RestaurantHandler.Utilities;
using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models;

public class AddressDetails
{

    [Required]
    public Guid Id { get; set; }

    /// <summary>Chiffrée au repos ; renseignée uniquement pour le propriétaire de l'adresse.</summary>
    public string? Street { get; set; }

    [Required]
    public string City { get; set; }

    [Required]
    public string Country { get; set; }

    [Required]
    public string ZipCode { get; set; }

    /// <summary>Chiffré au repos ; renseigné uniquement pour le propriétaire de l'adresse.</summary>
    public string? AdditionalInfo { get; set; }

    public float? Longitude { get; set; }

    public float? Latitude { get; set; }

    public Guid ProfileId { get; set; }
    [Required(ErrorMessage = "Le type est requis")]
    public Guid TypeId { get; set; }


    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public AddressDetails() { }

    /// <param name="minimal">
    /// Vrai pour tout lecteur autre que le propriétaire : rue et complément ne sont pas exposés.
    /// </param>
    public AddressDetails(Address address, bool minimal = false)
    {
        Id = address.Id;
        City = address.City;
        ZipCode = address.ZipCode;
        Country = address.Country;
        Longitude = address.Longitude;
        Latitude = address.Latitude;
        TypeId = address.TypeId;
        if (minimal)
        {
            return;
        }
        Street = address.Street;
        AdditionalInfo = address.AdditionalInfo;
        ProfileId = address.UserId;
        CreatedAt = address.CreatedAt;
        UpdatedAt = address.UpdatedAt;
    }

}

public class AddressCreate
{

    [Required(ErrorMessage = "La rue est requise")]
    [StringLength(128, ErrorMessage = "La rue ne peut pas dépasser 128 caractères")]
    public string Street { get; set; }

    [Required(ErrorMessage = "La ville est requise")]
    [StringLength(64, ErrorMessage = "La ville ne peut pas dépasser 64 caractères")]
    public string City { get; set; }

    [Required(ErrorMessage = "Le pays est requis")]
    [StringLength(64, ErrorMessage = "Le pays ne peut pas dépasser 64 caractères")]
    public string Country { get; set; }

    [Required(ErrorMessage = "Le code postal est requis")]
    [StringLength(16, ErrorMessage = "Le code postal ne peut pas dépasser 16 caractères")]
    public string ZipCode { get; set; }

    [StringLength(200, ErrorMessage = "Les informations supplémentaires ne peuvent pas dépasser 200 caractères")]
    public string? AdditionalInfo { get; set; }

    [Range(-180, 180, ErrorMessage = "La longitude doit être comprise entre -180 et 180")]
    public float? Longitude { get; set; }

    [Range(-90, 90, ErrorMessage = "La latitude doit être comprise entre -90 et 90")]
    public float? Latitude { get; set; }

    [Required(ErrorMessage = "L'identifiant utilisateur est requis")]
    public Guid UserId { get; set; }
    [Required(ErrorMessage = "Le type est requis")]
    public Guid TypeId { get; set; }
}

public class AddressUpdate
{
    [Required]
    public Guid Id { get; set; }

    [Required(ErrorMessage = "La rue est requise")]
    [StringLength(128, ErrorMessage = "La rue ne peut pas dépasser 128 caractères")]
    public string Street { get; set; }

    [Required(ErrorMessage = "La ville est requise")]
    [StringLength(64, ErrorMessage = "La ville ne peut pas dépasser 64 caractères")]
    public string City { get; set; }

    [Required(ErrorMessage = "Le pays est requis")]
    [StringLength(64, ErrorMessage = "Le pays ne peut pas dépasser 64 caractères")]
    public string Country { get; set; }

    [Required(ErrorMessage = "Le code postal est requis")]
    [StringLength(16, ErrorMessage = "Le code postal ne peut pas dépasser 16 caractères")]
    public string ZipCode { get; set; }

    [StringLength(200, ErrorMessage = "Les informations supplémentaires ne peuvent pas dépasser 200 caractères")]
    public string? AdditionalInfo { get; set; }

    [Range(-180, 180, ErrorMessage = "La longitude doit être comprise entre -180 et 180")]
    public float? Longitude { get; set; }

    [Range(-90, 90, ErrorMessage = "La latitude doit être comprise entre -90 et 90")]
    public float? Latitude { get; set; }

    [Required(ErrorMessage = "Le type est requis")]
    public Guid TypeId { get; set; }
}
