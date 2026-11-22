namespace RestaurantHandler.Models.Filters;

/// <summary>
/// Filtres pour la liste paginée des utilisateurs (administration).
/// </summary>
public class FilterUsers
{
    /// <summary>Recherche sur le prénom et/ou le nom de l'utilisateur.</summary>
    public string? Name { get; set; }

    /// <summary>Filtre sur un rôle (forme historique, cumulée avec <see cref="RoleIds"/>).</summary>
    public Guid? RoleId { get; set; }

    /// <summary>Utilisateurs portant au moins un de ces rôles.</summary>
    public List<Guid>? RoleIds { get; set; }

    /// <summary>Comptes créés à partir de cette date (incluse).</summary>
    public DateTimeOffset? CreatedFrom { get; set; }

    /// <summary>Comptes créés avant cette date (exclue).</summary>
    public DateTimeOffset? CreatedTo { get; set; }

    /// <summary>Index du premier élément (pagination).</summary>
    public int First { get; set; }

    /// <summary>Nombre d'éléments par page (10 par défaut).</summary>
    public int? Row { get; set; }
}
