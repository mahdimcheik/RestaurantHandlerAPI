namespace RestaurantHandler.Models.Filters;

/// <summary>
/// Filtres pour la gestion des utilisateurs (liste des enseignants paginée).
/// </summary>
public class FilterUserManagement
{
    /// <summary>Recherche sur le prénom et/ou le nom de l'enseignant.</summary>
    public string? Name { get; set; }

    /// <summary>Filtre sur le statut de confirmation de l'enseignant.</summary>
    public bool? IsConfirmed { get; set; }

    /// <summary>Filtre sur le rôle de l'utilisateur.</summary>
    public Guid? RoleId { get; set; }

    /// <summary>Index du premier élément (pagination).</summary>
    public int First { get; set; }

    /// <summary>Nombre d'éléments par page (10 par défaut).</summary>
    public int? Row { get; set; }
}
