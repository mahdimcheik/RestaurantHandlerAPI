using System.ComponentModel;
using RestaurantHandler.Contexts;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace RestaurantHandler.Mcp;

/// <summary>
/// Cours d'un prof, en lecture seule et anonyme. Seuls les profs que la recherche montre (actifs,
/// non archivés) sont visibles : <see cref="CursusService"/> ne filtre pas sur le statut du prof.
/// </summary>
[McpServerToolType]
public sealed class TeacherCourseTools
{
    public const string GetTeacherCoursesName = "get_teacher_courses";
    public const string GetTeacherCoursesDescription =
        "Liste les cours proposés par un professeur BonProf (intitulé, description, niveau, catégories, "
        + "prix horaire), à partir de l'identifiant renvoyé par fast_search. "
        + "Lists the courses offered by a BonProf teacher, given the teacher id returned by fast_search.";

    private const int DescriptionMaxLength = 1000;

    [McpServerTool(
        Name = GetTeacherCoursesName,
        Title = "Cours d'un professeur",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true
    )]
    [Description(GetTeacherCoursesDescription)]
    public static async Task<TeacherCoursesResponse> GetTeacherCourses(
        MainContext context,
        CursusService cursusService,
        RichTextSanitizer sanitizer,
        [Description("Identifiant du professeur (champ id de fast_search). Teacher id from fast_search.")]
            Guid teacherId
    )
    {
        var teacher = await context
            .Users.AsNoTracking()
            .Where(u =>
                u.Id == teacherId
                && u.Teacher != null
                && u.ArchivedAt == null
                && u.StatusId == HardCode.ACCOUNT_ACTIVE
            )
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .FirstOrDefaultAsync();

        if (teacher is null)
            throw new McpException("Professeur introuvable. Teacher not found.");

        var cursuses = (await cursusService.GetCursusesByTeacherIdAsync(teacher.Id)).Data ?? [];

        return new TeacherCoursesResponse(
            teacher.Id,
            teacher.FirstName,
            teacher.LastName,
            McpPublicLinks.ProfileUrl(teacher.Id),
            cursuses
                .Select(c => new TeacherCourse(
                    c.Id,
                    c.Name,
                    sanitizer.Excerpt(c.Description, DescriptionMaxLength),
                    c.Price,
                    c.Level?.Name,
                    c.Categories.Select(cat => cat.Name).ToList()
                ))
                .ToList()
        );
    }
}

public sealed record TeacherCoursesResponse(
    Guid TeacherId,
    string FirstName,
    string LastName,
    [property: Description("Lien vers le profil public du prof, où l'on réserve.")] string ProfileUrl,
    List<TeacherCourse> Courses
);

public sealed record TeacherCourse(
    Guid Id,
    string Name,
    [property: Description("Description en texte brut, tronquée.")] string? Description,
    [property: Description("Prix horaire en euros.")] decimal Price,
    string? Level,
    List<string> Categories
);
