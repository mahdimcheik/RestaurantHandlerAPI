using RestaurantHandler.Contexts;
using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

/// <summary>
/// Profils : lecture d'un profil par identifiant (<see cref="GetProfileByIdAsync"/>) et mise à
/// jour du profil de l'utilisateur connecté.
/// </summary>
public class ProfileService
{
    private readonly MainContext _context;
    private readonly UserManager<UserApp> _userManager;
    private readonly StorageService _storage;

    public ProfileService(MainContext context, UserManager<UserApp> userManager, StorageService storage)
    {
        _context = context;
        _userManager = userManager;
        _storage = storage;
    }

    public async Task<Response<UserDetails>> GetProfileByIdAsync(
        Guid id,
        bool includeRoles = true,
        bool includeStatus = false,
        bool onlyActive = false,
        string notFoundMessage = "Utilisateur non trouvé",
        string successMessage = "Utilisateur récupéré avec succès"
    )
    {
        var query = _context.Users.AsQueryable();
        if (includeStatus)
        {
            query = query.Include(u => u.Status);
        }

        var user = await query.FirstOrDefaultAsync(u =>
            u.Id == id && (!onlyActive || u.ArchivedAt == null)
        );

        if (user is null)
        {
            return new Response<UserDetails> { Status = 404, Message = notFoundMessage };
        }

        if (user.ImgUrl is not null)
        {
            user.ImgUrl = _storage.Url(user.ImgUrl);
        }

        var roles = includeRoles
            ? await CheckUser.GetRoles(user, _context, _userManager)
            : null;

        return new Response<UserDetails>
        {
            Status = 200,
            Message = successMessage,
            Data = new UserDetails(user, roles),
        };
    }

    public async Task<Response<UserDetails>> UpdateMyProfileAsync(
        UserUpdate userUpdate,
        CallerIdentity? caller
    )
    {
        var user = caller is null
            ? null
            : await _context.Users.Include(u => u.Status).FirstOrDefaultAsync(u => u.Id == caller.UserId);
        if (user is null)
        {
            return new Response<UserDetails> { Status = 401, Message = "Utilisateur non authentifié" };
        }

        userUpdate.UpdateUser(user);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();

        if (user.ImgUrl is not null)
        {
            user.ImgUrl = _storage.Url(user.ImgUrl);
        }

        var roles = await CheckUser.GetRoles(user, _context, _userManager);
        return new Response<UserDetails>
        {
            Status = 200,
            Message = "Profil mis à jour avec succès",
            Data = new UserDetails(user, roles),
        };
    }
}
