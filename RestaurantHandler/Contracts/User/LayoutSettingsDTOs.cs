using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models;

public class LayoutSettingsDetails
{
    public string? Preset { get; set; }
    public string? Primary { get; set; }
    public string? Surface { get; set; }
    public bool DarkTheme { get; set; }
    public string? MenuMode { get; set; }

    public LayoutSettingsDetails() { }

    public LayoutSettingsDetails(UserLayoutSettings settings)
    {
        Preset = settings.Preset;
        Primary = settings.Primary;
        Surface = settings.Surface;
        DarkTheme = settings.DarkTheme;
        MenuMode = settings.MenuMode;
    }
}

public class LayoutSettingsUpdate
{
    [MaxLength(32)]
    public string? Preset { get; set; }

    [MaxLength(32)]
    public string? Primary { get; set; }

    [MaxLength(32)]
    public string? Surface { get; set; }

    public bool DarkTheme { get; set; }

    [RegularExpression("^(static|overlay)$", ErrorMessage = "Le mode du menu doit être « static » ou « overlay »")]
    public string? MenuMode { get; set; }

    public UserLayoutSettings ToEntity() =>
        new()
        {
            Preset = Preset,
            Primary = Primary,
            Surface = Surface,
            DarkTheme = DarkTheme,
            MenuMode = MenuMode,
        };
}
