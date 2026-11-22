using RestaurantHandler.Contexts;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using TerminalApi.Services;

namespace RestaurantHandler.Utilities;

public class SlotCleaningJob
{
    private readonly MainContext _context;

    public SlotCleaningJob(MainContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Archive (suppression logique) les créneaux passés qui ne portent aucune réservation
    /// vivante — règle R3 : jamais réservés, ou seulement par des réservations annulées ou
    /// refusées. Un créneau porteur d'une réservation en attente n'est pas touché : le
    /// balayage des commandes l'annule, et la purge suivante l'archive. Jamais de suppression
    /// physique : elle emporterait en cascade les réservations et leur historique.
    /// </summary>
    public void CleanSlots()
    {
        var now = DateTimeOffset.UtcNow;
        var archived = _context
            .Slots.Where(s =>
                s.ArchivedAt == null
                && s.DateFrom < now
                && !s.Reservations.Any(r =>
                    r.ArchivedAt == null
                    && TeacherAvailability.BlockingStatuses.Contains(r.StatusId)
                )
            )
            .ExecuteUpdate(u => u.SetProperty(s => s.ArchivedAt, now));
        Console.WriteLine($"CleanSlots archived {archived} slot(s)");
    }
}

public class HangfireJobsBootstrapper : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        RecurringJob.AddOrUpdate<SlotCleaningJob>(
            "DailyCleanSlots",
            job => job.CleanSlots(),
            Cron.Daily
        );

        // Restart-safe cleanup: release pending orders whose payment deadline has
        // passed. Backstops the precise "checkout.session.expired" webhook and also
        // catches orders abandoned before any Stripe checkout was created.
        RecurringJob.AddOrUpdate<JobChron>(
            "clean-stale-orders",
            job => job.CleanStalePendingOrders(),
            "*/5 * * * *"
        );

        // Settle teacher wallet balances: first mark the reservations whose dispute window has
        // elapsed PAYABLE, then one Stripe transfer per PAYABLE reservation's not-yet-paid-out credits. Idempotent via the payout link plus a
        // reservation-derived Stripe key, so the 15-minute cadence simply controls how promptly
        // teachers are paid — and keeps any retry well inside Stripe's 24h deduplication window.
        RecurringJob.AddOrUpdate<JobChron>(
            "payout-teacher-balances",
            job => job.PayoutTeacherBalances(),
            "*/15 * * * *"
        );

        // Issue the deferred student refunds: one Stripe refund per PENDING Refund row whose
        // 48h cooling window (from cancellation) has elapsed. Idempotent via the reservation-derived
        // key, so the cadence only controls how promptly cancelled bookings are reimbursed.
        RecurringJob.AddOrUpdate<JobChron>(
            "process-pending-refunds",
            job => job.ProcessPendingRefunds(),
            "*/15 * * * *"
        );

        // No-payment flow (Order.PaymentRequired == false): auto-reject a PENDING
        // reservation the teacher never answered within TEACHER_RESPONSE_WINDOW (48h),
        // freeing the slot.
        RecurringJob.AddOrUpdate<JobChron>(
            "expire-stale-no-payment-reservations",
            job => job.ExpireStaleNoPaymentReservations(),
            "*/5 * * * *"
        );

        // No-payment flow: since no payout ever runs for these reservations, they need
        // their own time-based path to DONE (no proof-of-delivery step — nothing
        // financial is at stake) so a review can later attach to them (Feature 13).
        RecurringJob.AddOrUpdate<JobChron>(
            "complete-no-payment-reservations",
            job => job.CompleteNoPaymentReservations(),
            "*/15 * * * *"
        );

        // RGPD : suppression des fichiers de justificatifs prof arrivés en fin de conservation.
        RecurringJob.AddOrUpdate<JobChron>(
            "purge-expired-privacy-documents",
            job => job.PurgeExpiredPrivacyDocuments(),
            Cron.Daily
        );

        // Drop the previous per-reservation transfer job if it's still registered.
        RecurringJob.RemoveIfExists("pay-passed-reservations");

        // Drop the old in-memory job-cleanup recurring entry if it's still registered.
        RecurringJob.RemoveIfExists("delete-passed-jobs");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
