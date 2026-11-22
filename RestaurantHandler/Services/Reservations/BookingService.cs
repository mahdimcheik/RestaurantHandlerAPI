using RestaurantHandler.Contexts;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;
using TerminalApi.Services;

namespace RestaurantHandler.Services;

public class BookingService(
    MainContext context,
    NotificationDispatcher notifications,
    ReservationCartService cart,
    JobChron jobChron
)
{
    public async Task<Response<ReservationDetails>> BookSlotAsync(
        ReservationCreate reservationCreate,
        CallerIdentity? principals
    )
    {
        var student = CheckUser.GetUser(principals, context);
        if (student is null)
        {
            return new Response<ReservationDetails>
            {
                Status = 403,
                Message = "Utilisateur non trouvé",
            };
        }
        if (!student.EmailConfirmed)
        {
            return new Response<ReservationDetails>
            {
                Status = 403,
                Message = AccountActivation.RequiredToBookMessage,
            };
        }
        var slot = await context
            .Slots.Include(s =>
                s.Reservations.Where(r =>
                    r.ArchivedAt == null
                    && TeacherAvailability.BlockingStatuses.Contains(r.StatusId)
                )
            )
            .Include(s => s.Teacher.User)
            .FirstOrDefaultAsync(s => s.Id == reservationCreate.SlotId && s.ArchivedAt == null);
        // Occupé = toute réservation vivante (TeacherAvailability.BlockingStatuses, règle R1),
        // la même définition que la recherche. Un créneau commencé n'est plus réservable.
        if (
            slot is null
            || slot.Reservations.Count != 0
            || slot.DateFrom <= DateTimeOffset.UtcNow
        )
        {
            return new Response<ReservationDetails>
            {
                Status = 404,
                Message = "Créneau non trouvé ou déjà réservé",
                Data = null,
            };
        }

        // R5 : un élève ne réserve pas un créneau de son propre compte professeur.
        if (slot.TeacherId == student.Id)
        {
            return new Response<ReservationDetails>
            {
                Status = 400,
                Message = "Vous ne pouvez pas réserver votre propre créneau",
                Data = null,
            };
        }

        // Un compte prof non actif bloque toujours la réservation, quel que soit le flux
        // paiement.
        if (
            slot.Teacher is null
            || slot.Teacher.User is null
            || slot.Teacher.User.StatusId != HardCode.ACCOUNT_ACTIVE
        )
        {
            return new Response<ReservationDetails>
            {
                Status = 400,
                Message = "Ce professeur n'est pas disponible à la réservation pour le moment",
                Data = null,
            };
        }

        // Un prof pas encore pleinement onboardé (SIRET, Stripe Connect et/ou vérification
        // documentaire) reste réservable, mais sans paiement en ligne : le prof valide ou
        // refuse manuellement chaque réservation, aucun argent ne transite par la
        // plateforme (cf. Order.PaymentRequired).
        var paymentRequired =
            EnvironmentVariables.PAYMENTS_ENABLED
            && slot.Teacher.IsProfessional
            && slot.Teacher.IsStripeConfirmed
            && slot.Teacher.IsVerified;

        // Le cursus doit appartenir au prof du créneau. Depuis le split, le prof est
        // porté par Cursus.TeacherId, donc ce contrôle est un simple prédicat : sans
        // lui, un élève réserve le créneau du prof A avec le cursus du prof B — ou un
        // id inexistant, ce qui figeait autrefois UnitPrice à 0 (cours gratuit).
        var cursus = await context.Cursuses.FirstOrDefaultAsync(p =>
            p.Id == reservationCreate.CursusId
            && p.TeacherId == slot.TeacherId
            && p.ArchivedAt == null
        );
        if (cursus is null)
        {
            return new Response<ReservationDetails>
            {
                Status = 400,
                Message = "Ce cursus n'est pas proposé par ce professeur",
                Data = null,
            };
        }

        var activeOrder = await cart.GetActiveOrder(student.Id, slot.TeacherId, paymentRequired);

        Reservation newReservation = new Reservation(reservationCreate, activeOrder.Id);
        context.Reservations.Add(newReservation);
        newReservation.Cursus = cursus;
        newReservation.CreatedAt = DateTimeOffset.UtcNow;

        // Fige le prix au moment de la réservation : tarif horaire, montant brut
        // (= tarif × durée) et taux de commission. Tout le reste du flux argent
        // (checkout, crédit wallet, payout, remboursement) lit ce snapshot et ne
        // recalcule jamais depuis Cursus, dont le prix peut changer ensuite.
        var lengthInHours =
            (decimal)(slot.DateTo - slot.DateFrom).Ticks / TimeSpan.TicksPerHour;
        newReservation.UnitPrice = cursus.Price;
        newReservation.CommissionRate = HardCode.PLATFORM_COMMISSION_RATE;
        newReservation.Amount = decimal.Round(
            newReservation.UnitPrice * lengthInHours,
            2,
            MidpointRounding.AwayFromZero
        );

        // Première ligne d'historique : null -> PENDING (créé par l'élève).
        newReservation.RecordStatusCreation(student.Id, "Réservation créée");

        // Commande sans paiement : le prof doit valider/refuser manuellement, sous
        // peine d'auto-rejet passé ce délai (JobChron.ExpireStaleNoPaymentReservations).
        if (!activeOrder.PaymentRequired)
        {
            newReservation.TeacherResponseDeadline = DateTimeOffset.UtcNow.Add(
                HardCode.TEACHER_RESPONSE_WINDOW
            );
        }

        await context.SaveChangesAsync();

        // update order
        activeOrder.TotalAmount += newReservation.Amount;
        activeOrder.UpdatedAt = DateTimeOffset.UtcNow;
        activeOrder.OrderDate = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync();

        await notifications.NotifyAsync(
            new NotificationRequest(
                slot.TeacherId,
                slot.Teacher.User.Email,
                NotificationTypeEnum.Reservation,
                "Nouvelle réservation en attente de validation",
                "#ffabcf",
                "pi pi-plus",
                NotificationLinks.Reservation(newReservation.Id, forTeacher: true)
            )
        );

        // Commande sans paiement : jamais de checkout Stripe, donc rien à expirer/planifier
        // ici — le seul délai qui compte est TeacherResponseDeadline, posé plus haut.
        if (activeOrder.PaymentRequired)
        {
            // Adding a reservation changes the order, so any open checkout is now stale.
            try
            {
                if (activeOrder.CheckoutID is not null)
                {
                    await jobChron.ExpireCheckout(activeOrder.CheckoutID);
                    activeOrder.CheckoutID = null;
                }
            }
            catch { }

            // Give the (unpaid) order a cleanup deadline so the recurring sweep releases
            // the held slots if the buyer never completes checkout. Aligned to the same
            // window Stripe will enforce once a checkout session is created.
            activeOrder.PaymentDeadline = DateTimeOffset.UtcNow.Add(HardCode.CHECKOUT_WINDOW);
            await context.SaveChangesAsync();
        }

        return new Response<ReservationDetails>
        {
            Status = 201,
            Message = "Réservation créé avec succès",
            Data = new ReservationDetails(newReservation),
        };
    }

    // Seule action exposée par cet endpoint : le prof propriétaire du créneau accepte une
    // réservation encore PENDING (flux sans paiement — l'acceptation du flux payant vient
    // exclusivement du webhook Stripe checkout.session.completed). Toute autre transition
    // passe par un endpoint dédié (RemoveReservationByTeacherAsync pour le refus,
    // ValidateReservationAsync pour DELIVERED, DisputeService pour les litiges).
    public async Task UpdateReservationStatusAsync(
        ReservationUpdateStatus updateStatus,
        CallerIdentity? principals
    )
    {
        var teacher = CheckUser.GetUser(principals, context);
        if (teacher is null)
        {
            throw new ResourceNotFoundException("Utilisateur non trouvé");
        }

        var reservation = await context
            .Reservations.Include(r => r.Status)
            .Include(r => r.Slot)
            .Include(r => r.Student.User)
            .Include(r => r.Order)
            .Where(r => r.Slot.TeacherId == teacher.Id)
            .FirstOrDefaultAsync(r => r.Id == updateStatus.ReservationId);

        if (reservation is null)
        {
            throw new ResourceNotFoundException("Réservation non trouvée");
        }

        if (
            reservation.StatusId != HardCode.RESERVATION_PENDING
            || updateStatus.StatusCode != StatusReservationCode.Accepted
            || reservation.Order is null
            || reservation.Order.PaymentRequired
        )
        {
            throw new BusinessConflictException(
                "Seule l'acceptation d'une réservation en attente est autorisée ici"
            );
        }

        var newStatus = await context.StatusReservations.FirstOrDefaultAsync(s =>
            s.Code == updateStatus.StatusCode
        );

        if (newStatus is null)
        {
            throw new InvalidOperationException("Nouveau statut non trouvé");
        }

        reservation.TransitionStatusTo(context, newStatus.Id, teacher.Id, "Acceptée par le prof");
        reservation.TeacherResponseDeadline = null;

        await context.SaveChangesAsync();

        await notifications.NotifyAsync(
            new NotificationRequest(
                reservation.StudentId,
                reservation.Student.User.Email,
                NotificationTypeEnum.Reservation,
                "Votre réservation a été acceptée",
                "#ffabcf",
                "pi pi-pencil",
                NotificationLinks.Reservation(reservation.Id, forTeacher: false)
            )
        );
    }
}
