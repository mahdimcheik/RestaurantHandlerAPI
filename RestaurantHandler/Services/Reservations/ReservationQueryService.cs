using System.Globalization;
using System.Text.Json;
using RestaurantHandler.Contexts;
using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

/// <summary>
/// Viewer perspective for the unified reservation listing — drives the base scope,
/// which name filters apply and which avatars are hydrated.
/// </summary>
public enum ReservationViewRole
{
    Student,
    Teacher,
    Admin,
}

public class ReservationQueryService(
    MainContext context,
    ILogger<ReservationQueryService> logger,
    StorageService storage
)
{
    public async Task<Response<List<SlotDetails>>> GetReservationByStudentAsync(
        DateTimeOffset dateFrom,
        DateTimeOffset dateTo,
        CallerIdentity? principals
    )
    {
        var student = CheckUser.GetUser(principals, context);

        if (student is null)
        {
            return new Response<List<SlotDetails>>
            {
                Status = 404,
                Message = "Enseignant non trouvé",
                Data = null,
            };
        }

        if (dateFrom >= dateTo)
        {
            return new Response<List<SlotDetails>>
            {
                Status = 400,
                Message = "La date de fin doit être postérieure à la date de début",
                Data = null,
            };
        }
        var slots = await context
            .Slots.Where(s => s.DateFrom > dateFrom && s.DateTo < dateTo && s.Reservations != null)
            .Where(s =>
                s.Reservations.Any(r =>
                    r.StudentId == student.Id
                    && r.ArchivedAt == null
                    && TeacherAvailability.BlockingStatuses.Contains(r.StatusId)
                )
            )
            .Include(s =>
                s.Reservations.Where(r =>
                    r.ArchivedAt == null
                    && TeacherAvailability.BlockingStatuses.Contains(r.StatusId)
                )
            )
                .ThenInclude(r => r.Status)
            .Include(s =>
                s.Reservations.Where(r =>
                    r.ArchivedAt == null
                    && TeacherAvailability.BlockingStatuses.Contains(r.StatusId)
                )
            )
                .ThenInclude(r => r.Cursus)
            .Include(s => s.Teacher)
                .ThenInclude(t => t.User)
            .Include(s => s.Type)
            .Select(s => new SlotDetails(s, true))
            .ToListAsync();

        try
        {
            var teachers = slots.Select(x => x.Teacher).Distinct().ToList();
            foreach (var teacher in teachers)
            {
                if (
                    teacher?.User?.ImgUrl is not null
                    && !string.IsNullOrEmpty(teacher?.User?.ImgUrl)
                )
                {
                    teacher.User.ImgUrl = storage.Url(teacher.User.ImgUrl);
                }
            }
        }
        catch { }

        return new Response<List<SlotDetails>>
        {
            Status = 200,
            Message = "Créneaux disponibles récupérés avec succès",
            Data = slots,
            Count = slots.Count,
        };
    }

    public Task<Response<List<ReservationDetails>>> GetReservationByStudentPaginated(
        CustomTableState query,
        CallerIdentity? principals
    ) => GetReservationsPaginated(query, principals, ReservationViewRole.Student);

    public Task<Response<List<ReservationDetails>>> GetReservationByTeacherPaginated(
        CustomTableState query,
        CallerIdentity? principals
    ) => GetReservationsPaginated(query, principals, ReservationViewRole.Teacher);

    public Task<Response<List<ReservationDetails>>> GetReservationByAdminPaginated(
        CustomTableState query
    ) => GetReservationsPaginated(query, null, ReservationViewRole.Admin);

    /// <summary>
    /// Nombre de réservations en attente de validation par le prof connecté
    /// (statut <c>PENDING</c>, non archivées) — sert à afficher un badge côté prof.
    /// </summary>
    public async Task<Response<int>> GetPendingReservationsCountByStudentAsync(
        CallerIdentity? principals
    )
    {
        var user = CheckUser.GetUser(principals, context);
        if (user is null)
        {
            return new Response<int>
            {
                Status = 404,
                Message = "Utilisateur non trouvé",
                Data = 0,
            };
        }

        var count = await context.Reservations.CountAsync(r =>
            r.StudentId == user.Id
            && r.ArchivedAt == null
            && r.StatusId == HardCode.RESERVATION_PENDING
        );

        return new Response<int>
        {
            Status = 200,
            Message = "Nombre de réservations en attente récupéré avec succès",
            Data = count,
            Count = count,
        };
    }

    /// <summary>
    /// Unified, paginated reservation listing shared by the student, teacher and admin
    /// endpoints. The <paramref name="role"/> drives the base scope, which name filters
    /// apply and which avatars are hydrated:
    /// <list type="bullet">
    /// <item>Student — own reservations, may filter by teacher name.</item>
    /// <item>Teacher — reservations on own slots, may filter by student name.</item>
    /// <item>Admin — all reservations, may filter by both.</item>
    /// </list>
    /// Common filters (status, slot date range, global search) apply to every role.
    /// </summary>
    private async Task<Response<List<ReservationDetails>>> GetReservationsPaginated(
        CustomTableState query,
        CallerIdentity? principals,
        ReservationViewRole role
    )
    {
        IQueryable<Reservation> queryable;

        if (role == ReservationViewRole.Admin)
        {
            queryable = context.Reservations.Where(r => r.ArchivedAt == null);
        }
        else
        {
            var user = CheckUser.GetUser(principals, context);

            if (user is null)
            {
                return new Response<List<ReservationDetails>>
                {
                    Status = 404,
                    Message = "Utilisateur non trouvé",
                    Data = null,
                };
            }

            queryable =
                role == ReservationViewRole.Teacher
                    ? context.Reservations.Where(r =>
                        r.Slot.TeacherId == user.Id && r.ArchivedAt == null
                    )
                    : context.Reservations.Where(r =>
                        r.StudentId == user.Id && r.ArchivedAt == null
                    );
        }

        IQueryable<Reservation> reservations = queryable
            .Include(r => r.Cursus)
            .Include(r => r.Slot)
                .ThenInclude(s => s.Teacher)
                    .ThenInclude(t => t.User)
            .Include(r => r.Slot)
                .ThenInclude(s => s.Type)
            .Include(r => r.Student)
                .ThenInclude(s => s.User)
            .Include(r => r.Status);

        // teacher name filter — student & admin views
        if (
            role != ReservationViewRole.Teacher
            && query.Filters.TryGetValue("teacherName", out Filter teacherName)
        )
        {
            reservations = reservations.Where(r =>
                EF.Functions.ILike(
                    r.Slot.Teacher.User.FirstName.ToLower()
                        + " "
                        + r.Slot.Teacher.User.LastName.ToLower(),
                    $"%{teacherName.Value.ToString().Trim()}%"
                )
            );
        }

        // student name filter — teacher & admin views
        if (
            role != ReservationViewRole.Student
            && query.Filters.TryGetValue("studentName", out Filter studentName)
        )
        {
            reservations = reservations.Where(r =>
                EF.Functions.ILike(
                    r.Student.User.FirstName.ToLower() + " " + r.Student.User.LastName.ToLower(),
                    $"%{studentName.Value.ToString().Trim()}%"
                )
            );
        }

        // status filter
        if (query.Filters?.TryGetValue("status", out Filter? statuses) == true && statuses != null)
        {
            var ids = JsonSerializer.Deserialize<Guid[]>(statuses.Value?.ToString() ?? "[]");
            if (ids != null && ids.Length > 0)
                reservations = reservations.Where(r => ids.Contains(r.StatusId));
        }

        // slot date range: slot must start after dateFrom and end before dateTo
        if (
            query.Filters.TryGetValue("dateFrom", out Filter dateFromFilter)
            && DateTimeOffset.TryParse(
                dateFromFilter.Value?.ToString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dateFrom
            )
        )
        {
            reservations = reservations.Where(r => r.Slot.DateFrom > dateFrom);
        }

        if (
            query.Filters.TryGetValue("dateTo", out Filter dateToFilter)
            && DateTimeOffset.TryParse(
                dateToFilter.Value?.ToString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dateTo
            )
        )
        {
            reservations = reservations.Where(r => r.Slot.DateTo < dateTo);
        }

        // global filter
        if (!string.IsNullOrEmpty(query.Search?.Trim()))
        {
            var trimmed = query.Search.Trim();
            reservations = reservations.Where(r =>
                EF.Functions.ILike(r.Description, $"%{trimmed}%")
                || EF.Functions.ILike(r.Title, $"%{trimmed}%")
            );
        }

        // count
        var count = await reservations.CountAsync();

        // pagination
        if (query.Rows == 0 || query.Rows < 0)
        {
            query.Rows = 10;
        }
        var results = await reservations
            .OrderByDescending(r => r.Slot.DateFrom)
            .ThenBy(r => r.Id)
            .Skip(query.First)
            .Take(query.Rows)
            .ToListAsync();

        // storage.Url is not idempotent (a URL is not a valid key → null): each tracked User
        // must be resolved exactly once, even when it appears as student and as teacher.
        var users = results
            .Select(r => r.Student?.User)
            .Concat(results.Select(r => r.Slot?.Teacher?.User))
            .Where(u => u is not null)
            .Distinct();
        foreach (var u in users)
        {
            u!.ImgUrl = storage.Url(u.ImgUrl);
        }

        return new Response<List<ReservationDetails>>
        {
            Status = 200,
            Message = "Réservations récupérées avec succès",
            Data = results.Select(r => new ReservationDetails(r)).ToList(),
            Count = count,
        };
    }

    // reservations
    public async Task<Response<ReservationDetails>> GetReservationById(
        Guid reservationId,
        CallerIdentity? principals
    )
    {
        var user = CheckUser.GetUser(principals, context);
        if (user is null)
        {
            return new Response<ReservationDetails>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
            };
        }

        var isAdmin = principals?.IsAdmin == true;
        if (
            !isAdmin && !await ReservationAccess.IsParticipantAsync(context, reservationId, user.Id)
        )
        {
            return ReservationNotFound();
        }

        var reservation = await context
            .Reservations.Where(r => r.Id == reservationId)
            .Include(r => r.Slot)
                .ThenInclude(s => s.Teacher)
                    .ThenInclude(t => t.User)
            .Include(r => r.Slot)
                .ThenInclude(s => s.Type)
            .Include(r => r.Student)
                .ThenInclude(s => s.User)
            .Include(r => r.Status)
            .Include(r => r.Order)
            .Include(r => r.StatusHistory)
                .ThenInclude(h => h.FromStatus)
            .Include(r => r.StatusHistory)
                .ThenInclude(h => h.ToStatus)
            .Include(r => r.StatusHistory)
                .ThenInclude(h => h.ChangedBy)
            .FirstOrDefaultAsync();

        if (reservation is null)
        {
            return ReservationNotFound();
        }

        if (isAdmin)
        {
            logger.LogInformation(
                "Consultation administrateur de la réservation {ReservationId} par {UserId}",
                reservationId,
                user.Id
            );
        }

        if (reservation is not null && !string.IsNullOrEmpty(reservation?.Student?.User?.ImgUrl))
        {
            reservation.Student.User.ImgUrl = storage.Url(reservation.Student.User.ImgUrl);
        }

        if (
            reservation is not null
            && !string.IsNullOrEmpty(reservation?.Slot?.Teacher?.User?.ImgUrl)
        )
        {
            reservation.Slot.Teacher.User.ImgUrl = storage.Url(
                reservation.Slot.Teacher.User.ImgUrl
            );
        }

        // Le code de présence n'est remis qu'à l'élève, et à partir du début du créneau :
        // un professeur qui le verrait pourrait le saisir seul (ADR-0012). C'est le seul
        // endroit où il est exposé.
        var details = new ReservationDetails(reservation);
        if (
            reservation.Student?.UserId == user.Id
            && reservation.Slot?.DateFrom <= DateTimeOffset.UtcNow
        )
        {
            details.ValidationCode = reservation.ValidationCode;
        }

        return new Response<ReservationDetails>
        {
            Status = 200,
            Message = "Réservations récupérées avec succès",
            Data = details,
        };
    }

    private static Response<ReservationDetails> ReservationNotFound() =>
        new() { Status = 404, Message = "Réservation introuvable" };
}
