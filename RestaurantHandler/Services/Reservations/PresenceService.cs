using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

public class PresenceService(MainContext context, NotificationDispatcher notifications)
{
    /// <summary>
    /// Valide une réservation via le code de présence remis à l'élève (chemin
    /// heureux de la <c>proof of delivery</c>). Réservé au prof propriétaire du
    /// créneau et aux admins. Transition <c>ACCEPTED → DELIVERED</c>.
    /// </summary>
    public async Task<
        Response<(ReservationDetails? Reservation, DateTimeOffset? UnlockTime)>
    > ValidateReservationAsync(ReservationValidate dto, CallerIdentity? principals)
    {
        var user = CheckUser.GetUser(principals, context);
        if (user is null)
        {
            return new Response<(ReservationDetails? Reservation, DateTimeOffset? UnlockTime)>
            {
                Status = 403,
                Message = "Utilisateur non trouvé",
                Data = (null, null),
            };
        }

        var reservation = await context
            .Reservations.Include(r => r.Slot)
            .ThenInclude(s => s.Teacher)
            .Include(r => r.Student.User)
            .Include(r => r.Status)
            .Include(r => r.Order)
            .FirstOrDefaultAsync(r => r.Id == dto.ReservationId && r.ArchivedAt == null);

        if (reservation is null)
        {
            return new Response<(ReservationDetails? Reservation, DateTimeOffset? UnlockTime)>
            {
                Status = 404,
                Message = "Réservation non trouvée",
                Data = (null, null),
            };
        }

        // Un admin valide n'importe quelle réservation ; un prof seulement les
        // siennes (créneau dont il est propriétaire).
        var isAdmin =
            principals?.IsAdmin == true;
        if (!isAdmin && reservation.Slot?.Teacher?.UserId != user.Id)
        {
            return new Response<(ReservationDetails? Reservation, DateTimeOffset? UnlockTime)>
            {
                Status = 404,
                Message = "Réservation introuvable",
                Data = (null, null),
            };
        }

        // Seule une réservation payée (ACCEPTED) peut être marquée livrée, et seulement entre
        // le début du créneau et l'échéance de contestation (ADR-0012) : avant, le cours n'a
        // pas eu lieu ; après, la réservation est versable et le signal n'a plus d'usage.
        var now = DateTimeOffset.UtcNow;
        if (
            reservation.StatusId != HardCode.RESERVATION_ACCEPTED
            || reservation.Order is not { PaymentRequired: true }
            || reservation.Slot is null
            || now < reservation.Slot.DateFrom
            || now > reservation.Slot.DateTo + HardCode.DISPUTE_WINDOW
        )
        {
            return new Response<(ReservationDetails? Reservation, DateTimeOffset? UnlockTime)>
            {
                Status = 400,
                Message = "La réservation n'est pas dans un état validable",
                Data = (null, null),
            };
        }

        // Verrou anti-bruteforce : après VALIDATION_CODE_MAX_ATTEMPTS essais ratés, on
        // bloque les tentatives pendant VALIDATION_CODE_LOCKOUT. La DB est la source de
        // vérité (restart-safe) et le verrou cible la réservation attaquée.
        if (reservation.ValidationLockedUntil is { } lockedUntil && lockedUntil > now)
        {
            DateTimeOffset localTime = lockedUntil.ToLocalTime();
            return new Response<(ReservationDetails? Reservation, DateTimeOffset? UnlockTime)>
            {
                Status = 429,
                Message = $"Trop de tentatives. Réessayez après {localTime:HH:mm} (UTC +1).",
                Data = (null, lockedUntil),
            };
        }

        if (reservation.ValidationCode is null || reservation.ValidationCode != dto.Code)
        {
            reservation.ValidationAttempts++;
            if (reservation.ValidationAttempts >= HardCode.VALIDATION_CODE_MAX_ATTEMPTS)
            {
                reservation.ValidationLockedUntil = now + HardCode.VALIDATION_CODE_LOCKOUT;
                reservation.ValidationAttempts = 0;
                await context.SaveChangesAsync();
                return new Response<(ReservationDetails? Reservation, DateTimeOffset? UnlockTime)>
                {
                    Status = 429,
                    Message =
                        "Trop de tentatives. Validation temporairement bloquée, réessayez plus tard.",
                    Data = (null, null),
                };
            }

            await context.SaveChangesAsync();
            return new Response<(ReservationDetails? Reservation, DateTimeOffset? UnlockTime)>
            {
                Status = 400,
                Message = "Code de validation invalide",
                Data = (null, null),
            };
        }

        // Bon code : on réinitialise le compteur et le verrou.
        reservation.ValidationAttempts = 0;
        reservation.ValidationLockedUntil = null;

        reservation.TransitionStatusTo(
            context,
            HardCode.RESERVATION_DELIVERED,
            user.Id,
            "Cours confirmé via code de présence"
        );
        await context.SaveChangesAsync();

        await notifications.NotifyAsync(
            new NotificationRequest(
                reservation.StudentId,
                reservation.Student.User.Email,
                NotificationTypeEnum.Reservation,
                "Votre cours a été confirmé",
                "#34c759",
                "pi pi-check",
                NotificationLinks.Reservation(reservation.Id, forTeacher: false)
            )
        );

        return new Response<(ReservationDetails? Reservation, DateTimeOffset? UnlockTime)>
        {
            Status = 200,
            Message = "Réservation validée avec succès",
            Data = (new ReservationDetails(reservation), null),
        };
    }
}
