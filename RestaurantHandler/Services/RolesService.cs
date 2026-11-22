using Microsoft.EntityFrameworkCore;
using RestaurantHandler.Contexts;
using RestaurantHandler.Models;

namespace RestaurantHandler.Services;

/// <summary>
/// Service pour la gestion des rôles
/// </summary>
public class RolesService(MainContext context)
{
    /// <summary>
    /// Récupère tous les rôles
    /// </summary>
    /// <returns>Liste des rôles</returns>
    public async Task<Response<List<RoleDetails>>> GetAllRolesAsync()
    {
        var roles = await context
            .Roles
            .AsNoTracking()
            .Where(r => r.ArchivedAt == null)
            .OrderBy(r => r.Name)
            .Select(r => new RoleDetails(r))
            .ToListAsync();

        return new Response<List<RoleDetails>>
        {
            Status = 200,
            Message = "Rôles récupérés avec succès",
            Data = roles,
            Count = roles.Count,
        };
    }
}
