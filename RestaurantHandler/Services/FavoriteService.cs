using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Services.Interfaces;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

/// <summary>
/// Service pour la gestion des enseignants favoris d'un élève
/// </summary>
public class FavoriteService(MainContext context) : IFavoriteService
{
    /// <summary>
    /// Ajoute un enseignant aux favoris de l'élève authentifié
    /// </summary>
    public async Task<Response<FavoriteDetails>> AddFavoriteAsync(
        CreateFavorite dto,
        CallerIdentity? principal
    )
    {
        var user = CheckUser.GetUser(principal, context);
        if (user is null)
        {
            return new Response<FavoriteDetails>
            {
                Status = 404,
                Message = "L'utilisateur n'existe pas",
                Data = null,
            };
        }

        // L'identifiant de l'élève provient des claims, pas du DTO
        var student = await context.Students.FirstOrDefaultAsync(s => s.Id == user.Id);
        if (student is null)
        {
            return new Response<FavoriteDetails>
            {
                Status = 404,
                Message = "L'élève n'existe pas",
                Data = null,
            };
        }

        var teacherExists = await context.Teachers.AnyAsync(t => t.Id == dto.TeacherId);
        if (!teacherExists)
        {
            return new Response<FavoriteDetails>
            {
                Status = 404,
                Message = "L'enseignant n'existe pas",
                Data = null,
            };
        }

        // Empêcher les doublons
        var alreadyFavorite = await context.Favorites.AnyAsync(f =>
            f.StudentId == student.Id && f.TeacherId == dto.TeacherId
        );
        if (alreadyFavorite)
        {
            return new Response<FavoriteDetails>
            {
                Status = 400,
                Message = "Cet enseignant est déjà dans vos favoris",
                Data = null,
            };
        }

        var favorite = new Favorite
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            TeacherId = dto.TeacherId,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        context.Favorites.Add(favorite);
        await context.SaveChangesAsync();

        return new Response<FavoriteDetails>
        {
            Status = 201,
            Message = "Enseignant ajouté aux favoris avec succès",
            Data = new FavoriteDetails(favorite),
        };
    }

    /// <summary>
    /// Retire un enseignant des favoris de l'élève authentifié
    /// </summary>
    public async Task<Response<object>> RemoveFavoriteAsync(
        Guid teacherId,
        CallerIdentity? principal
    )
    {
        var user = CheckUser.GetUser(principal, context);
        if (user is null)
        {
            return new Response<object>
            {
                Status = 404,
                Message = "L'utilisateur n'existe pas",
                Data = null,
            };
        }

        var favorite = await context.Favorites.FirstOrDefaultAsync(f =>
            f.StudentId == user.Id && f.TeacherId == teacherId
        );

        if (favorite is null)
        {
            return new Response<object>
            {
                Status = 404,
                Message = "Favori non trouvé",
                Data = null,
            };
        }

        context.Favorites.Remove(favorite);
        await context.SaveChangesAsync();

        return new Response<object>
        {
            Status = 200,
            Message = "Enseignant retiré des favoris avec succès",
            Data = null,
        };
    }

    /// <summary>
    /// Indique si un enseignant fait partie des favoris de l'élève authentifié
    /// </summary>
    public async Task<Response<bool>> IsFavoriteAsync(Guid teacherId, CallerIdentity? principal)
    {
        var user = CheckUser.GetUser(principal, context);
        if (user is null)
        {
            return new Response<bool>
            {
                Status = 404,
                Message = "L'utilisateur n'existe pas",
                Data = false,
            };
        }

        var isFavorite = await context.Favorites.AnyAsync(f =>
            f.StudentId == user.Id && f.TeacherId == teacherId
        );

        return new Response<bool>
        {
            Status = 200,
            Message = "Vérification effectuée avec succès",
            Data = isFavorite,
        };
    }
}
