using System.ComponentModel.DataAnnotations;
using RestaurantHandler.Models.Interfaces;

namespace RestaurantHandler.Models;

/// <summary>
/// Paramètres globaux de la plateforme, sur une seule ligne d'Id
/// <see cref="RestaurantHandler.Utilities.HardCode.PLATFORM_SETTINGS_ID"/>.
/// </summary>
public class PlatformSettings : BaseModel
{
    /// <summary>
    /// Mode maintenance : l'API répond 503 à tout sauf à l'accès minimal d'un Manager
    /// (cf. MaintenanceMiddleware), le front n'affiche que la page d'accueil.
    /// </summary>
    public bool MaintenanceEnabled { get; set; }

    [MaxLength(1000)]
    public string? MaintenanceMessage { get; set; }

    public Guid? UpdatedById { get; set; }
}
