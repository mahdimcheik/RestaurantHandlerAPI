using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

/// <summary>
/// Service pour la gestion des utilisateurs côté Manager.
/// </summary>
public class UserManagementService
{
    private readonly MainContext _context;

    public UserManagementService(MainContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Statuts de compte autorisés pour une modification manuelle par l'administration.
    /// </summary>
    private static readonly HashSet<Guid> AllowedAccountStatuses =
    [
        HardCode.ACCOUNT_ACTIVE,
        HardCode.ACCOUNT_PENDING,
        HardCode.ACCOUNT_BANNED,
    ];

    /// <summary>
    /// Modifie manuellement le statut du compte d'un utilisateur (actif, en attente, banni).
    /// Un Manager ne peut ni écarter son propre compte ni le dernier Manager actif.
    /// </summary>
    public async Task<Response<UserDetails>> SetUserAccountStatusAsync(Guid userId, Guid statusId, Guid callerId)
    {
        if (!AllowedAccountStatuses.Contains(statusId))
        {
            return new Response<UserDetails>
            {
                Status = 400,
                Message = "Statut de compte invalide",
            };
        }

        var user = await _context
            .Users.Include(u => u.Status)
            .FirstOrDefaultAsync(u => u.Id == userId && u.ArchivedAt == null);
        if (user is null)
        {
            return new Response<UserDetails>
            {
                Status = 404,
                Message = "Utilisateur non trouvé",
            };
        }

        if (statusId != HardCode.ACCOUNT_ACTIVE)
        {
            if (userId == callerId)
            {
                return new Response<UserDetails>
                {
                    Status = 400,
                    Message = "Vous ne pouvez pas changer le statut de votre propre compte",
                };
            }

            var isManager = await _context.UserRoles.AnyAsync(ur =>
                ur.UserId == userId && ur.RoleId == HardCode.ROLE_MANAGER
            );
            var otherActiveManager = await _context.Users.AnyAsync(u =>
                u.Id != userId
                && u.ArchivedAt == null
                && u.StatusId == HardCode.ACCOUNT_ACTIVE
                && _context.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == HardCode.ROLE_MANAGER)
            );
            if (isManager && !otherActiveManager)
            {
                return new Response<UserDetails>
                {
                    Status = 400,
                    Message = "Impossible d'écarter le dernier Manager actif",
                };
            }
        }

        user.StatusId = statusId;
        user.UpdatedAt = DateTime.UtcNow;

        // Le jeton d'accès déjà émis reste valable jusqu'à son expiration ; sans jeton de
        // renouvellement, la session s'arrête là.
        if (statusId == HardCode.ACCOUNT_BANNED)
        {
            await _context.RefreshTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync();
        }

        await _context.SaveChangesAsync();

        // Recharge la navigation Status pour refléter le nouveau statut dans la réponse.
        await _context.Entry(user).Reference(u => u.Status).LoadAsync();

        return new Response<UserDetails>
        {
            Status = 200,
            Message = "Statut du compte mis à jour avec succès",
            Data = new UserDetails(user, null),
        };
    }
}
