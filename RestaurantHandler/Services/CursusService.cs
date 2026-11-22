using RestaurantHandler.Models;
using Microsoft.EntityFrameworkCore;
using RestaurantHandler.Contexts;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Services;

public class CursusService(MainContext context, NotificationService notificationService, NotificationDispatcher notifications, RichTextSanitizer sanitizer)
{
    /// <summary>Description nettoyée, ou le message d'erreur si elle est vide ou trop longue.</summary>
    private (string? Clean, string? Error) CleanDescription(string? html)
    {
        var clean = sanitizer.Sanitize(html);
        if (clean is null)
            return (null, "La description est requise");
        if (sanitizer.TextLength(clean) > HardCode.CURSUS_DESCRIPTION_MAX_LENGTH)
            return (null, $"La description ne peut pas dépasser {HardCode.CURSUS_DESCRIPTION_MAX_LENGTH} caractères");
        return (clean, null);
    }

    /// <summary>
    /// Taxonomie du cursus : niveau + catégories. Toujours chargée ensemble, sinon
    /// <see cref="CursusDetails"/> renvoie un niveau null et des catégories vides.
    /// </summary>
    private static IQueryable<Cursus> WithTaxonomy(IQueryable<Cursus> cursuses) =>
        cursuses.Include(p => p.Level).Include(p => p.Categories);

    /// <summary>
    /// Matérialise puis projette, au lieu de <c>Select(p =&gt; new CursusDetails(p))</c>
    /// dans la requête : un <c>Include</c> de collection combiné à une projection est
    /// silencieusement ignoré par EF, et <see cref="CursusDetails.Categories"/>
    /// reviendrait vide sans le moindre message.
    /// </summary>
    private static async Task<List<CursusDetails>> ToDetailsAsync(IQueryable<Cursus> cursuses) =>
        (await cursuses.ToListAsync()).Select(p => new CursusDetails(p)).ToList();

    /// <summary>
    /// Résout les catégories demandées, ou explique lesquelles manquent : un id inconnu
    /// est une erreur, pas un silence.
    /// </summary>
    private async Task<(List<CategoryCursus>? categories, string? error)> ResolveCategoriesAsync(
        List<Guid> categoryIds
    )
    {
        if (categoryIds is null || categoryIds.Count == 0)
            return (new List<CategoryCursus>(), null);

        var categories = await context.CategoryCursuses
            .Where(c => categoryIds.Contains(c.Id) && c.ArchivedAt == null)
            .ToListAsync();

        if (categories.Count != categoryIds.Distinct().Count())
            return (null, "Une ou plusieurs catégories non trouvées");

        return (categories, null);
    }

    public async Task<Response<List<CursusDetails>>> GetAllCursusesAsync()
    {
        var cursuses = await ToDetailsAsync(
            WithTaxonomy(context.Cursuses.AsNoTracking())
                .Where(p => p.ArchivedAt == null)
                .OrderByDescending(p => p.CreatedAt)
        );

        return new Response<List<CursusDetails>>
        {
            Status = 200,
            Message = "Cursus récupérés avec succès",
            Data = cursuses,
            Count = cursuses.Count
        };
    }

    public async Task<Response<CursusDetails>> GetCursusByIdAsync(Guid id)
    {
        var cursus = await WithTaxonomy(context.Cursuses.AsNoTracking())
            .FirstOrDefaultAsync(p => p.Id == id && p.ArchivedAt == null);

        if (cursus == null)
        {
            return new Response<CursusDetails>
            {
                Status = 404,
                Message = "Cursus non trouvé",
                Data = null
            };
        }

        return new Response<CursusDetails>
        {
            Status = 200,
            Message = "Cursus récupéré avec succès",
            Data = new CursusDetails(cursus)
        };
    }

    /// <summary>
    /// Cursus d'un niveau donné : chaque cursus porte son propre niveau.
    /// </summary>
    public async Task<Response<List<CursusDetails>>> GetCursusesByLevelIdAsync(Guid levelId)
    {
        var cursuses = await ToDetailsAsync(
            WithTaxonomy(context.Cursuses.AsNoTracking())
                .Where(p => p.LevelId == levelId && p.ArchivedAt == null)
                .OrderByDescending(p => p.CreatedAt)
        );

        return new Response<List<CursusDetails>>
        {
            Status = 200,
            Message = "Cursus du niveau récupérés avec succès",
            Data = cursuses,
            Count = cursuses.Count
        };
    }

    /// <summary>
    /// Cursus d'un prof. Filtre direct sur <see cref="Cursus.TeacherId"/>.
    /// </summary>
    public async Task<Response<List<CursusDetails>>> GetCursusesByTeacherIdAsync(Guid teacherId)
    {
        var cursuses = await ToDetailsAsync(
            WithTaxonomy(context.Cursuses.AsNoTracking())
                .Where(p => p.TeacherId == teacherId && p.ArchivedAt == null)
                .OrderByDescending(p => p.CreatedAt)
        );

        return new Response<List<CursusDetails>>
        {
            Status = 200,
            Message = "Cursus du professeur récupérés avec succès",
            Data = cursuses,
            Count = cursuses.Count
        };
    }

    public async Task<Response<CursusDetails>> CreateCursusAsync(CursusCreate cursusDto, CallerIdentity? User)
    {
        var user = CheckUser.GetUser(User, context);
        if(user is null)
        {
            return new Response<CursusDetails>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
                Data = null
            };
        }

        // Seul un prof vend. L'ancien code s'appuyait sur la recherche du cursus
        // possédé pour l'établir implicitement ; sans elle, un élève tomberait sur une
        // violation de FK vers Teachers (500) au lieu d'un refus lisible.
        var isTeacher = await context.Teachers.AnyAsync(t => t.Id == user.Id);
        if (!isTeacher)
        {
            return new Response<CursusDetails>
            {
                Status = 403,
                Message = "Seul un professeur peut créer un cursus",
                Data = null
            };
        }

        var levelExists = await context.LevelCursuses.AnyAsync(l =>
            l.Id == cursusDto.LevelId && l.ArchivedAt == null
        );
        if (!levelExists)
        {
            return new Response<CursusDetails>
            {
                Status = 404,
                Message = "Niveau non trouvé",
                Data = null
            };
        }

        var (categories, categoryError) = await ResolveCategoriesAsync(cursusDto.CategoryIds);
        if (categoryError is not null)
        {
            return new Response<CursusDetails>
            {
                Status = 404,
                Message = categoryError,
                Data = null
            };
        }

        var (description, descriptionError) = CleanDescription(cursusDto.Description);
        if (description is null)
        {
            return new Response<CursusDetails>
            {
                Status = 400,
                Message = descriptionError!,
                Data = null
            };
        }
        cursusDto.Description = description;

        var cursus = new Cursus(cursusDto, user.Id, categories!);

        context.Cursuses.Add(cursus);
        await context.SaveChangesAsync();
        await notifications.NotifyAsync(
            new NotificationRequest(
                user.Id,
                user.Email,
                NotificationTypeEnum.Cursus,
                $"Cursus créé : {cursus.Name}",
                "#ffabcf",
                "pi pi-plus",
                NotificationLinks.TeacherCursuses
            )
        );

        // Recharger avec les relations pour la réponse
        var createdCursus = await WithTaxonomy(context.Cursuses).FirstAsync(p => p.Id == cursus.Id);

        return new Response<CursusDetails>
        {
            Status = 201,
            Message = "Cursus créé avec succès",
            Data = new CursusDetails(createdCursus)
        };
    }

    public async Task<Response<CursusDetails>> UpdateCursusAsync(CursusUpdate cursusDto, CallerIdentity? User)
    {
        var user = CheckUser.GetUser(User, context);
        if (user is null)
        {
            return new Response<CursusDetails>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
                Data = null
            };
        }

        // Categories chargées : UpdateCursus remplace la collection en place.
        var cursus = await context.Cursuses
            .Include(p => p.Categories)
            .FirstOrDefaultAsync(p => p.Id == cursusDto.Id && p.ArchivedAt == null);

        if (cursus == null)
        {
            return new Response<CursusDetails>
            {
                Status = 404,
                Message = "Cursus non trouvé",
                Data = null
            };
        }

        // Le prix d'un cursus décide de ce que paie un élève : seul son prof y touche.
        if (cursus.TeacherId != user.Id)
        {
            return new Response<CursusDetails>
            {
                Status = 404,
                Message = "Cursus non trouvé",
                Data = null
            };
        }

        var levelExists = await context.LevelCursuses.AnyAsync(l =>
            l.Id == cursusDto.LevelId && l.ArchivedAt == null
        );
        if (!levelExists)
        {
            return new Response<CursusDetails>
            {
                Status = 404,
                Message = "Niveau non trouvé",
                Data = null
            };
        }

        var (categories, categoryError) = await ResolveCategoriesAsync(cursusDto.CategoryIds);
        if (categoryError is not null)
        {
            return new Response<CursusDetails>
            {
                Status = 404,
                Message = categoryError,
                Data = null
            };
        }

        var (description, descriptionError) = CleanDescription(cursusDto.Description);
        if (description is null)
        {
            return new Response<CursusDetails>
            {
                Status = 400,
                Message = descriptionError!,
                Data = null
            };
        }
        cursusDto.Description = description;

        cursusDto.UpdateCursus(cursus, categories!);

        await context.SaveChangesAsync();

        // Recharger avec les relations pour la réponse
        var updatedCursus = await WithTaxonomy(context.Cursuses).FirstAsync(p => p.Id == cursusDto.Id);

        return new Response<CursusDetails>
        {
            Status = 200,
            Message = "Cursus mis à jour avec succès",
            Data = new CursusDetails(updatedCursus)
        };
    }

    public async Task<Response<object>> DeleteCursusAsync(Guid id, CallerIdentity? User)
    {
        var user = CheckUser.GetUser(User, context);
        if (user is null)
        {
            return new Response<object>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
                Data = null
            };
        }

        var cursus = await context.Cursuses
            .FirstOrDefaultAsync(p => p.Id == id && p.ArchivedAt == null);

        if (cursus == null)
        {
            return new Response<object>
            {
                Status = 404,
                Message = "Cursus non trouvé",
                Data = null
            };
        }

        if (cursus.TeacherId != user.Id)
        {
            return new Response<object>
            {
                Status = 404,
                Message = "Cursus non trouvé",
                Data = null
            };
        }

        // Archivage, pas de suppression : les réservations déjà passées pointent
        // toujours ce cursus (leur prix est figé, mais le nom est lu à l'affichage).
        cursus.ArchivedAt = DateTimeOffset.UtcNow;
        cursus.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync();

        return new Response<object>
        {
            Status = 200,
            Message = "Cursus supprimé avec succès",
            Data = null
        };
    }
}
