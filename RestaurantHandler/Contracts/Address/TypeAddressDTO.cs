using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models;

public class TypeAddressDetails
{
    [Required]
    public Guid Id { get; set; }
    [Required]
    public string Name { get; set; }

    [Required]
    public string Color { get; set; }
    public string? Icon { get; set; }
    public string DisplayName { get; set; }

    /// <summary>
    /// Traductions du nom par langue (clé = code langue minuscule, ex. "fr"/"en")
    /// </summary>
    [Required]
    public Dictionary<string, string> Translations { get; set; }
    public TypeAddressDetails()
    {
    }
    public TypeAddressDetails(TypeAddress typeAddress)
    {
        Id = typeAddress.Id;
        Name = typeAddress.Name;
        Color = typeAddress.Color;
        Icon = typeAddress.Icon;
        DisplayName = typeAddress.DisplayName;
        Translations = typeAddress.Translations;
    }
}
