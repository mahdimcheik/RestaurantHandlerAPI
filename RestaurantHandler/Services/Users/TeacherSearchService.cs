using RestaurantHandler.Contexts;
using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Models;
using RestaurantHandler.Models.Filters;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

public class TeacherSearchService
{
    private readonly MainContext _context;
    private readonly StorageService _storage;

    public TeacherSearchService(MainContext context, StorageService storage)
    {
        _context = context;
        _storage = storage;
    }

    public async Task<Response<List<UserDetails>>> GetAllTeacherProfilesAsync(
        FilterTeacher filters,
        CallerIdentity? principal
    )
    {
        var query = _context
            .Users
            .Where(u =>
                u.Teacher != null
                && u.ArchivedAt == null
                && u.StatusId == HardCode.ACCOUNT_ACTIVE
            );

        if (filters.FullName is not null)
        {
            query = query.Where(u =>
                (u.FirstName.ToLower() + " " + u.LastName.ToLower()).Contains(
                    filters.FullName.ToLower()
                )
                || (u.LastName + " " + u.FirstName).Contains(filters.FullName)
                || u.Email.Contains(filters.FullName)
            );
        }

        if (filters.InFavorites == true)
        {
            var userId = CheckUser.GetUser(principal, _context)?.Id;
            query = query.Where(u =>
                _context.Favorites.Any(f => f.StudentId == userId && f.TeacherId == u.Id)
            );
        }

        var count = await query.CountAsync();

        var take = filters.Row != 0 ? filters.Row ?? 10 : 10;
        var skip = filters.Row != 0 ? filters.First : 0;
        var users = await query
            .AsNoTracking()
            .Include(u => u.Teacher)
            .OrderByDescending(u => u.CreatedAt)
            .ThenBy(u => u.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        foreach (var user in users)
        {
            if (user.ImgUrl is not null)
            {
                user.ImgUrl = _storage.Url(user.ImgUrl);
            }
        }

        return new Response<List<UserDetails>>
        {
            Status = 200,
            Message = "Profils enseignants récupérés avec succès",
            Data = users.Select(u => new UserDetails(u, null) { Email = "" }).ToList(),
            Count = count,
        };
    }

    /// <summary>
    /// Widget « profs mis en avant » de la page d'accueil : les derniers profs actifs inscrits.
    /// </summary>
    public async Task<Response<List<UserDetails>>> GetFeaturedTeachersAsync()
    {
        var users = await _context
            .Users.AsNoTracking()
            .Include(u => u.Teacher)
            .Where(u =>
                u.Teacher != null
                && u.ArchivedAt == null
                && u.StatusId == HardCode.ACCOUNT_ACTIVE
            )
            .OrderByDescending(u => u.CreatedAt)
            .Take(HardCode.FEATURED_TEACHERS_COUNT)
            .ToListAsync();

        foreach (var user in users)
        {
            if (user.ImgUrl is not null)
            {
                user.ImgUrl = _storage.Url(user.ImgUrl);
            }
        }

        return new Response<List<UserDetails>>
        {
            Status = 200,
            Message = "Profs mis en avant récupérés avec succès",
            Data = users.Select(u => new UserDetails(u, null) { Email = "" }).ToList(),
        };
    }
}
