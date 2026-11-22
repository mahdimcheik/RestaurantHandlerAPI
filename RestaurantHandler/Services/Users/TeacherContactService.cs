using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

public class TeacherContactService
{
    private readonly MainContext _context;
    private readonly MailService _mailService;

    public TeacherContactService(MainContext context, MailService mailService)
    {
        _context = context;
        _mailService = mailService;
    }

    /// <summary>
    /// Envoie un message d'un élève vers un enseignant par email. L'élève est
    /// identifié via ses claims ; l'enseignant est résolu par son identifiant.
    /// Le mail part de l'adresse no-reply de la plateforme, l'email de l'élève
    /// étant rappelé dans le corps pour permettre une réponse.
    /// </summary>
    public async Task<Response<object>> SendMessageToTeacherAsync(
        ContactTeacher dto,
        CallerIdentity? principal
    )
    {
        var student = CheckUser.GetUser(principal, _context);
        if (student is null)
        {
            return new Response<object>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
            };
        }

        var teacher = await _context.Users.FirstOrDefaultAsync(u =>
            u.Id == dto.TeacherId && u.Teacher != null && u.ArchivedAt == null
        );

        if (teacher is null || string.IsNullOrWhiteSpace(teacher.Email))
        {
            return new Response<object> { Status = 404, Message = "Enseignant non trouvé" };
        }

        // dto.Message est du HTML volontaire (éditeur riche côté client) : le template
        // l'insère tel quel ; le nom et l'email de l'élève sont encodés par Razor.
        await _mailService.SendTeacherContact(teacher, student, dto.Subject, dto.Message);

        return new Response<object> { Status = 200, Message = "Message envoyé avec succès" };
    }
}
