using RestaurantHandler.Models;
using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models;

public class StatusAccountDetails(StatusAccount status)
{
    public Guid Id => status.Id;
    public string Name => status.Name;
    public string Color => status.Color;
    public string? Icon => status.Icon;
    public Dictionary<string, string> Translations => status.Translations;
}
