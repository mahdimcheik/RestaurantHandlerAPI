using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models;

public class MaintenanceStatus
{
    [Required]
    public bool Enabled { get; set; }

    /// <summary>Message saisi par le SuperAdmin ; null : le front affiche son texte par défaut.</summary>
    public string? Message { get; set; }
}

public class MaintenanceUpdate
{
    [Required]
    public bool Enabled { get; set; }

    [MaxLength(1000)]
    public string? Message { get; set; }
}
