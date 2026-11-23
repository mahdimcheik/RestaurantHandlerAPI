using RestaurantHandler.Contexts;
using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Models;
using RestaurantHandler.Models.Filters;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

public class UserAdministrationService
{
    private readonly MainContext _context;
    private readonly UserManager<UserApp> _userManager;
    private readonly ProfileService _profileService;
    private readonly StorageService _storage;

    public UserAdministrationService(
        MainContext context,
        UserManager<UserApp> userManager,
        ProfileService profileService,
        StorageService storage
    )
    {
        _context = context;
        _userManager = userManager;
        _profileService = profileService;
        _storage = storage;
    }

    /// <summary>
    /// Fiche d'un utilisateur côté Manager : rôles, statut, date d'inscription et compteur de
    /// non-venues.
    /// </summary>
    public async Task<Response<UserAdminDetails>> GetUserByIdAsync(Guid id)
    {
        var user = await _context
            .Users.Include(u => u.Status)
            .FirstOrDefaultAsync(u => u.Id == id && u.ArchivedAt == null);
        if (user is null)
        {
            return new Response<UserAdminDetails> { Status = 404, Message = "Utilisateur non trouvé" };
        }

        if (user.ImgUrl is not null)
        {
            user.ImgUrl = _storage.Url(user.ImgUrl);
        }

        var roles = await CheckUser.GetRoles(user, _context, _userManager);
        return new Response<UserAdminDetails>
        {
            Status = 200,
            Message = "Utilisateur récupéré avec succès",
            Data = new UserAdminDetails(user, roles),
        };
    }

    /// <summary>
    /// Récupère la liste paginée des utilisateurs avec filtres (rôles, date de création, nom).
    /// </summary>
    public async Task<Response<List<UserAdminDetails>>> GetUsersAsync(FilterUsers filters)
    {
        var query = _context
            .Users.Include(u => u.Status)
            .Where(u => u.ArchivedAt == null);

        var roleIds = (filters.RoleIds ?? []).Concat(filters.RoleId is { } single ? [single] : []).Distinct().ToList();
        if (roleIds.Count > 0)
        {
            // Table UserRoles et non la navigation UserApp.UserRoles, mappée sur une clé fantôme jamais remplie.
            query = query.Where(u => _context.UserRoles.Any(ur => ur.UserId == u.Id && roleIds.Contains(ur.RoleId)));
        }

        if (filters.StatusId is { } statusId)
            query = query.Where(u => u.StatusId == statusId);

        if (filters.CreatedFrom is { } from)
            query = query.Where(u => u.CreatedAt >= from);
        if (filters.CreatedTo is { } to)
            query = query.Where(u => u.CreatedAt < to);

        if (!string.IsNullOrWhiteSpace(filters.Name))
        {
            var name = filters.Name.ToLower();
            query = query.Where(u =>
                (u.FirstName.ToLower() + " " + u.LastName.ToLower()).Contains(name)
                || (u.LastName.ToLower() + " " + u.FirstName.ToLower()).Contains(name)
                || u.Email.ToLower().Contains(name)
                || (u.PhoneNumber != null && u.PhoneNumber.Replace(" ", "").Contains(name.Replace(" ", "")))
            );
        }

        var count = await query.CountAsync();

        var users = await query
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.Id)
            .Skip(filters.First)
            .Take(filters.Row ?? 10)
            .ToListAsync();

        var data = new List<UserAdminDetails>(users.Count);
        foreach (var user in users)
        {
            if (user.ImgUrl is not null)
            {
                user.ImgUrl = _storage.Url(user.ImgUrl);
            }

            var roles = await CheckUser.GetRoles(user, _context, _userManager);
            data.Add(new UserAdminDetails(user, roles));
        }

        return new Response<List<UserAdminDetails>>
        {
            Status = 200,
            Message = "Utilisateurs récupérés avec succès",
            Data = data,
            Count = count,
        };
    }

    /// <summary>
    /// Crée un nouvel utilisateur et lui assigne son rôle.
    /// </summary>
    public async Task<Response<UserDetails>> CreateUserAsync(UserCreate newUserDTO)
    {
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == newUserDTO.RoleId);
        if (role?.Name is null)
        {
            return new Response<UserDetails> { Status = 400, Message = "Rôle inconnu" };
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        if (await _userManager.FindByEmailAsync(newUserDTO.Email) is not null)
        {
            return new Response<UserDetails>
            {
                Status = 400,
                Message = "L'email est déjà utilisé",
            };
        }

        var newUser = new UserApp(newUserDTO) { CreatedAt = DateTime.UtcNow };

        var result = await _userManager.CreateAsync(newUser, newUserDTO.Password);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync();
            return new Response<UserDetails>
            {
                Status = 400,
                Message = string.Join(" ", result.Errors.Select(e => e.Description)),
            };
        }

        await _userManager.AddToRoleAsync(newUser, role.Name);
        await transaction.CommitAsync();

        var created = AsDetails(await GetUserByIdAsync(newUser.Id));
        if (created.Status == 200)
        {
            created.Status = 201;
            created.Message = "Utilisateur créé avec succès";
        }
        return created;
    }

    /// <summary>
    /// Met à jour un utilisateur existant.
    /// </summary>
    public async Task<Response<UserDetails>> UpdateUserAsync(Guid id, UserUpdate update)
    {
        var user = await _context
            .Users.FirstOrDefaultAsync(u => u.Id == id && u.ArchivedAt == null);

        if (user is null)
        {
            return new Response<UserDetails>
            {
                Status = 404,
                Message = "Utilisateur non trouvé",
            };
        }

        update.UpdateUser(user);
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();

        var updated = AsDetails(await GetUserByIdAsync(user.Id));
        if (updated.Status == 200)
        {
            updated.Message = "Utilisateur mis à jour avec succès";
        }
        return updated;
    }

    private static Response<UserDetails> AsDetails(Response<UserAdminDetails> response) =>
        new()
        {
            Status = response.Status,
            Message = response.Message,
            Data = response.Data,
        };
}
