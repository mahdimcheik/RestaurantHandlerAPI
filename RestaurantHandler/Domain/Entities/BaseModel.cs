using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantHandler.Models.Interfaces;

public interface IArchivable
{
    public DateTimeOffset? ArchivedAt { get; set; }
}

public interface IUpdateable
{
    public DateTimeOffset? UpdatedAt { get; set; }
}

public interface ICreatable
{
    public DateTimeOffset CreatedAt { get; set; }
}

public abstract class BaseModel : IUpdateable, ICreatable, IArchivable
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [Column(TypeName = "timestamp with time zone")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column(TypeName = "timestamp with time zone")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [Column(TypeName = "timestamp with time zone")]
    public DateTimeOffset? ArchivedAt { get; set; }
}

public class BaseModelOption : BaseModel
{
    [Required]
    [MaxLength(64)]
    public string Name { get; set; }

    [Required]
    [MaxLength(16)]
    public string Color { get; set; }

    [MaxLength(256)]
    public string? Icon { get; set; }
}

/// <summary>Base pour les listes gérées par l'admin, ouvertes à l'exécution
/// (cf. CLAUDE.md Feature 14) — <see cref="Translations"/> stocke un libellé par
/// langue (clé = code langue minuscule, ex. "fr"/"en"), en plus de <see cref="BaseModelOption.Name"/>
/// qui reste l'identifiant stable/fallback. Ne pas utiliser pour les enums système
/// fermés (déjà traduits côté Angular i18n).</summary>
public class TranslatableModelOption : BaseModelOption
{
    public Dictionary<string, string> Translations { get; set; } = new();
}
