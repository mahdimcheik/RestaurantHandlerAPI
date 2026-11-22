using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models;

public class GenderDetails
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public Dictionary<string, string> Translations { get; set; } = new();

    public GenderDetails() { }

    public GenderDetails(Gender gender)
    {
        Id = gender.Id;
        Name = gender.Name;
        Color = gender.Color;
        Icon = gender.Icon;
        Translations = gender.Translations;
    }
}
