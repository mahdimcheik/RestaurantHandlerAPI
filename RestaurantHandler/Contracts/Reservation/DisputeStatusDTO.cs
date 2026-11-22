using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models;

public class DisputeStatusDetails(DisputeStatus status)
{
    [Required]
    public Guid Id => status.Id;

    [Required]
    public string Name => status.Name;

    [Required]
    public string Color => status.Color;

    public string? Icon => status.Icon;

    [Required]
    public Dictionary<string, string> Translations => status.Translations;
}

public class DisputeOutcomeDetails(DisputeOutcome outcome)
{
    [Required]
    public Guid Id => outcome.Id;

    [Required]
    public string Name => outcome.Name;

    [Required]
    public string Color => outcome.Color;

    public string? Icon => outcome.Icon;

    [Required]
    public Dictionary<string, string> Translations => outcome.Translations;
}
