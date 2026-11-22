namespace RestaurantHandler.Models;

/// <summary>Préférences visuelles de l'interface (thème, couleurs, mode du menu), stockées en jsonb.</summary>
public class UserLayoutSettings
{
    public string? Preset { get; set; }
    public string? Primary { get; set; }
    public string? Surface { get; set; }
    public bool DarkTheme { get; set; }
    public string? MenuMode { get; set; }
}
