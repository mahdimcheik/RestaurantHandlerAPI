using Microsoft.EntityFrameworkCore;
using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Services;

public class FormationsService(MainContext context, RichTextSanitizer sanitizer)
{
    /// <summary>Description nettoyée, ou le message d'erreur si elle est vide ou trop longue.</summary>
    private (string? Clean, string? Error) CleanDescription(string? html)
    {
        var clean = sanitizer.Sanitize(html);
        if (clean is null)
            return (null, "La description est requise");
        if (sanitizer.TextLength(clean) > HardCode.FORMATION_DESCRIPTION_MAX_LENGTH)
            return (null, $"La description ne peut pas dépasser {HardCode.FORMATION_DESCRIPTION_MAX_LENGTH} caractères");
        return (clean, null);
    }

    public async Task<Response<List<FormationDetails>>> GetAllFormationsAsync()
    {
        var formations = await context
            .Formations.AsNoTracking()
            .Where(f => f.ArchivedAt == null)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new FormationDetails(f))
            .ToListAsync();

        return new Response<List<FormationDetails>>
        {
            Status = 200,
            Message = "Formations récupérées avec succès",
            Data = formations,
            Count = formations.Count,
        };
    }


    public async Task<Response<FormationDetails>> GetFormationByIdAsync(Guid id)
    {
        var formation = await context
            .Formations.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id && f.ArchivedAt == null);

        if (formation == null)
        {
            return new Response<FormationDetails>
            {
                Status = 404,
                Message = "Formation non trouvée",
                Data = null,
            };
        }

        return new Response<FormationDetails>
        {
            Status = 200,
            Message = "Formation récupérée avec succès",
            Data = new FormationDetails(formation),
        };
    }

 
    public async Task<Response<List<FormationDetails>>> GetFormationsByUserIdAsync(Guid userId)
    {
        var formations = await context
            .Formations.AsNoTracking()
            .Where(f => f.UserId == userId && f.ArchivedAt == null)
            .OrderByDescending(f => f.DateFrom)
            .Select(f => new FormationDetails(f))
            .ToListAsync();

        return new Response<List<FormationDetails>>
        {
            Status = 200,
            Message = "Formations de l'utilisateur récupérées avec succès",
            Data = formations,
            Count = formations.Count,
        };
    }


    public async Task<Response<FormationDetails>> CreateFormationAsync(
        FormationCreate formationDto,
        CallerIdentity? User
    )
    {
        var user = CheckUser.GetUser(User, context);
        if (user is null)
        {
            return new Response<FormationDetails>
            {
                Status = 404,
                Message = "Utilisateur non trouvé",
                Data = null,
            };
        }

        // Validation des dates
        if (formationDto.DateTo.HasValue && formationDto.DateTo <= formationDto.DateFrom)
        {
            return new Response<FormationDetails>
            {
                Status = 400,
                Message = "La date de fin doit être postérieure à la date de début",
                Data = null,
            };
        }

        var (description, descriptionError) = CleanDescription(formationDto.Description);
        if (description is null)
        {
            return new Response<FormationDetails>
            {
                Status = 400,
                Message = descriptionError!,
                Data = null,
            };
        }
        formationDto.Description = description;

        var count = await context.Formations.Where(f => f.UserId == user.Id && f.ArchivedAt == null).CountAsync();
        if (count >= 5)
        {
            return new Response<FormationDetails>
            {
                Status = 403,
                Message = "Le nombre maximal de formations autorisé par utilisateur est de 5",
                Data = null,
            };
        }

        var formation = new Formation(formationDto, user.Id);

        context.Formations.Add(formation);
        await context.SaveChangesAsync();

        return new Response<FormationDetails>
        {
            Status = 201,
            Message = "Formation créée avec succès",
            Data = new FormationDetails(formation),
        };
    }

    public async Task<Response<FormationDetails>> UpdateFormationAsync(FormationUpdate formationDto, CallerIdentity? User)
    {
        var user = CheckUser.GetUser(User, context);
        if (user is  null)
        {
            return new Response<FormationDetails>
            {
                Status = 404,
                Message = "Utilisateur non trouvé",
                Data = null,
            };
        }
        var formation = await context.Formations.FirstOrDefaultAsync(f =>
            f.Id == formationDto.Id && f.UserId == user.Id && f.ArchivedAt == null
        );

        if (formation == null)
        {
            return new Response<FormationDetails>
            {
                Status = 404,
                Message = "Formation non trouvée",
                Data = null,
            };
        }
        // Validation des dates
        if (formationDto.DateTo.HasValue && formationDto.DateTo <= formationDto.DateFrom)
        {
            return new Response<FormationDetails>
            {
                Status = 400,
                Message = "La date de fin doit être postérieure à la date de début",
                Data = null,
            };
        }

        var (description, descriptionError) = CleanDescription(formationDto.Description);
        if (description is null)
        {
            return new Response<FormationDetails>
            {
                Status = 400,
                Message = descriptionError!,
                Data = null,
            };
        }
        formationDto.Description = description;

        formation.UpdateFormation(formationDto);

        await context.SaveChangesAsync();

        return new Response<FormationDetails>
        {
            Status = 200,
            Message = "Formation mise à jour avec succès",
            Data = new FormationDetails(formation),
        };
    }

    /// <summary>
    /// Archive une formation (suppression logique)
    /// </summary>
    /// <param name="id">Identifiant de la formation</param>
    /// <param name="principal">Session : seule une formation de l'appelant est archivée</param>
    /// <returns>Résultat de l'opération</returns>
    public async Task<Response<bool>> DeleteFormationAsync(Guid id, CallerIdentity? principal)
    {
        var user = CheckUser.GetUser(principal, context);
        if (user is null)
        {
            return new Response<bool>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
                Data = false,
            };
        }

        var formation = await context.Formations.FirstOrDefaultAsync(f =>
            f.Id == id && f.UserId == user.Id && f.ArchivedAt == null
        );

        if (formation == null)
        {
            return new Response<bool>
            {
                Status = 404,
                Message = "Formation non trouvée",
                Data = false,
            };
        }

        formation.ArchivedAt = DateTimeOffset.UtcNow;
        formation.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync();

        return new Response<bool>
        {
            Status = 200,
            Message = "Formation supprimée avec succès",
            Data = true,
        };
    }
}
