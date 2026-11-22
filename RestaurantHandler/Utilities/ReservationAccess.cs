using System.Security.Claims;
using RestaurantHandler.Contexts;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Utilities;

/// <summary>
/// Qui peut voir une réservation et sa conversation : son élève et son professeur. L'administration
/// lit aussi, mais n'écrit pas ; un tiers reçoit un 404, comme si la réservation n'existait pas.
/// </summary>
public static class ReservationAccess
{
    public static Task<bool> IsParticipantAsync(
        MainContext context,
        Guid reservationId,
        Guid userId
    ) =>
        context.Reservations.AnyAsync(r =>
            r.Id == reservationId
            && (
                (r.Student != null && r.Student.UserId == userId)
                || (r.Slot != null && r.Slot.Teacher != null && r.Slot.Teacher.UserId == userId)
            )
        );

    /// <summary>Vrai si le professeur a au moins une réservation avec cet élève.</summary>
    public static Task<bool> SharesReservationAsync(
        MainContext context,
        Guid studentUserId,
        Guid teacherUserId
    ) =>
        context.Reservations.AnyAsync(r =>
            r.Student != null
            && r.Student.UserId == studentUserId
            && r.Slot != null
            && r.Slot.Teacher != null
            && r.Slot.Teacher.UserId == teacherUserId
        );

    public static bool IsAdmin(ClaimsPrincipal principal) =>
        principal.IsInRole(nameof(RoleEnum.Admin)) || principal.IsInRole(nameof(RoleEnum.SuperAdmin));
}
