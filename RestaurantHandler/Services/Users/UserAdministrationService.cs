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
    private readonly RichTextSanitizer _sanitizer;

    public UserAdministrationService(
        MainContext context,
        UserManager<UserApp> userManager,
        ProfileService profileService,
        StorageService storage,
        RichTextSanitizer sanitizer
    )
    {
        _sanitizer = sanitizer;
        _context = context;
        _userManager = userManager;
        _profileService = profileService;
        _storage = storage;
    }

    /// <summary>
    /// Utilisateur par identifiant (forme historique de UsersService, côté admin).
    /// </summary>
    public Task<Response<UserDetails>> GetUserByIdAsync(Guid id) =>
        _profileService.GetProfileByIdAsync(
            id,
            includeRoles: true,
            includeStatus: true,
            includeTeacher: true,
            includeStudent: true,
            includeAddresses: true,
            includeFormations: true,
            onlyActive: true
        );

    /// <summary>
    /// Récupère la liste paginée des utilisateurs avec filtres (rôles, date de création, nom).
    /// </summary>
    public async Task<Response<List<UserAdminDetails>>> GetUsersAsync(FilterUsers filters)
    {
        var query = _context
            .Users.Include(u => u.Status)
            .Include(u => u.Gender)
            .Include(u => u.Teacher)
            .Include(u => u.Student)
            .Where(u => u.ArchivedAt == null);

        var roleIds = (filters.RoleIds ?? []).Concat(filters.RoleId is { } single ? [single] : []).Distinct().ToList();
        if (roleIds.Count > 0)
        {
            // Table UserRoles et non la navigation UserApp.UserRoles, mappée sur une clé fantôme jamais remplie.
            query = query.Where(u => _context.UserRoles.Any(ur => ur.UserId == u.Id && roleIds.Contains(ur.RoleId)));
        }

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
        newUser.Description = _sanitizer.Sanitize(newUser.Description);

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

        // Résolution du nom du rôle à partir de son identifiant.
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == newUserDTO.RoleId);
        var roleName = role?.Name ?? "Student";
        await _userManager.AddToRoleAsync(newUser, roleName);

        // Création du profil correspondant au rôle.
        if (newUserDTO.RoleId == HardCode.ROLE_TEACHER && newUserDTO.Teacher is not null)
        {
            var newTeacher = new Teacher(newUserDTO.Teacher, newUser.Id);
            await _context.Teachers.AddAsync(newTeacher);
            await TeacherVerificationHelper.CreateDefaultDocumentRequestsAsync(_context, newTeacher.Id);
        }
        else
        {
            await _context.Students.AddAsync(
                new Student { Id = newUser.Id, UserId = newUser.Id }
            );
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        var created = await GetUserByIdAsync(newUser.Id);
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
            .Users.Include(u => u.Teacher)
            .Include(u => u.Student)
            .Include(u => u.Languages)
            .FirstOrDefaultAsync(u => u.Id == id && u.ArchivedAt == null);

        if (user is null)
        {
            return new Response<UserDetails>
            {
                Status = 404,
                Message = "Utilisateur non trouvé",
            };
        }

        var languages = await _context
            .Languages.Where(l => update.LanguagesIds.Contains(l.Id))
            .ToListAsync();

        update.UpdateUser(user, languages);
        user.Description = _sanitizer.Sanitize(user.Description);
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();

        var updated = await GetUserByIdAsync(user.Id);
        if (updated.Status == 200)
        {
            updated.Message = "Utilisateur mis à jour avec succès";
        }
        return updated;
    }
}
