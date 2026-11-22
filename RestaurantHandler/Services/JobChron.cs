using RestaurantHandler.Contexts;
using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Models;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace TerminalApi.Services
{
    /// <summary>
    /// Restart-safe order housekeeping. There is NO in-memory job tracking: the only
    /// state is the Orders table. A recurring sweep releases pending orders whose
    /// <see cref="RestaurantHandler.Models.Order.PaymentDeadline"/> has passed, which survives
    /// any restart/redeploy and also catches orders abandoned before a Stripe checkout
    /// ever existed (those never produce a "checkout.session.expired" webhook).
    /// </summary>
    public class JobChron
    {
        private readonly MainContext _context;
        private readonly StripeConnectService _connectService;
        private readonly StripeRefundService _refundService;
        private readonly StorageService _storage;

        public JobChron(
            MainContext context,
            StripeConnectService connectService,
            StripeRefundService refundService,
            StorageService storage
        )
        {
            _context = context;
            _connectService = connectService;
            _refundService = refundService;
            _storage = storage;
        }

        /// <summary>
        /// Releases every pending order whose payment deadline has passed: expires the
        /// Stripe checkout if one is still open, cancels the held reservations (freeing the
        /// slots, history kept) and resets the order. Idempotent and safe to run on any schedule.
        /// </summary>
        public async Task CleanStalePendingOrders()
        {
            var now = DateTimeOffset.UtcNow;

            var staleOrders = await _context
                .Orders.Include(o => o.Reservations)
                .Where(o =>
                    o.StatusId == HardCode.STATUS_ORDER_PENDING
                    && o.PaymentDeadline != null
                    && o.PaymentDeadline < now
                )
                .ToListAsync();

            if (staleOrders.Count == 0)
                return;

            foreach (var order in staleOrders)
            {
                if (!string.IsNullOrEmpty(order.CheckoutID))
                {
                    // Best effort: the session may already be expired or completed.
                    await ExpireCheckout(order.CheckoutID!);
                }

                ReleasePendingOrder(_context, order, "Délai de paiement dépassé");
            }

            await _context.SaveChangesAsync();
            Console.WriteLine($"CleanStalePendingOrders released {staleOrders.Count} order(s)");
        }

        /// <summary>
        /// Vide une commande en attente dont le paiement n'aura pas lieu : ses réservations
        /// encore en attente passent <c>CANCELLED</c> (ligne et historique conservés) et sont
        /// détachées par <see cref="Order.Reset"/> — le checkout charge
        /// <c>Order.Reservations</c> sans filtrer le statut et les facturerait sinon. Ne
        /// sauvegarde pas. <paramref name="order"/> doit avoir ses réservations chargées.
        /// </summary>
        public static void ReleasePendingOrder(MainContext context, Order order, string reason)
        {
            foreach (var reservation in order.Reservations)
            {
                if (reservation.StatusId == HardCode.RESERVATION_PENDING)
                    reservation.TransitionStatusTo(context, HardCode.RESERVATION_CANCELLED, reason: reason);
            }

            order.Reset();
        }

        /// <summary>
        /// Auto-rejects reservations of a no-payment order (<see cref="Order.PaymentRequired"/>
        /// == false) the teacher never answered within
        /// <see cref="HardCode.TEACHER_RESPONSE_WINDOW"/> (<c>REJECTED</c>, detached from its
        /// order), freeing the slot. Restart-safe DB
        /// sweep, same shape as <see cref="CleanStalePendingOrders"/>.
        /// </summary>
        public async Task ExpireStaleNoPaymentReservations()
        {
            var now = DateTimeOffset.UtcNow;

            var staleReservations = await _context
                .Reservations.Include(r => r.Order)
                .ThenInclude(o => o.Reservations)
                .Where(r =>
                    r.StatusId == HardCode.RESERVATION_PENDING
                    && r.ArchivedAt == null
                    && r.TeacherResponseDeadline != null
                    && r.TeacherResponseDeadline < now
                )
                .ToListAsync();

            if (staleReservations.Count == 0)
                return;

            foreach (var reservation in staleReservations)
            {
                var order = reservation.Order;

                if (order != null)
                {
                    var newTotal = order.TotalAmount - reservation.Amount;
                    order.TotalAmount = newTotal >= 0 ? newTotal : 0;
                    order.UpdatedAt = now;

                    if (order.Reservations.All(r => r.Id == reservation.Id))
                    {
                        order.ArchivedAt = now;
                        order.PaymentDeadline = null;
                    }
                }

                reservation.TransitionStatusTo(
                    _context,
                    HardCode.RESERVATION_REJECTED,
                    reason: "Sans réponse du professeur"
                );
                reservation.TeacherResponseDeadline = null;
                reservation.Order = null;
                reservation.OrderId = null;
            }

            await _context.SaveChangesAsync();
            Console.WriteLine(
                $"ExpireStaleNoPaymentReservations rejected {staleReservations.Count} reservation(s)"
            );
        }

        /// <summary>
        /// No-payment reservations (<see cref="Order.PaymentRequired"/> == false) never
        /// generate a wallet credit, so they never reach <c>DONE</c> through
        /// <see cref="PayoutTeacherBalances"/> (the only other path to it). Nothing of
        /// financial value is at stake here, so completion is a pure time-based sweep: once
        /// the slot is over, the reservation is done. This is what lets a no-payment
        /// reservation later carry a review (Feature 13 requires <c>DONE</c>).
        /// </summary>
        public async Task CompleteNoPaymentReservations()
        {
            var now = DateTimeOffset.UtcNow;

            var reservations = await _context
                .Reservations.Include(r => r.Slot)
                .Include(r => r.Order)
                .Where(r =>
                    r.StatusId == HardCode.RESERVATION_ACCEPTED
                    && r.ArchivedAt == null
                    && r.Order != null
                    && !r.Order.PaymentRequired
                    && r.Slot.DateTo < now
                )
                .ToListAsync();

            if (reservations.Count == 0)
                return;

            foreach (var reservation in reservations)
            {
                reservation.TransitionStatusTo(
                    _context,
                    HardCode.RESERVATION_DONE,
                    reason: "Créneau terminé (sans paiement)"
                );
            }

            await _context.SaveChangesAsync();
            Console.WriteLine(
                $"CompleteNoPaymentReservations completed {reservations.Count} reservation(s)"
            );
        }

        /// <summary>
        /// Settles released teacher earnings via <b>one Stripe transfer per reservation</b>, net of
        /// whatever the teacher owes the platform. A wallet entry becomes payable once its lesson has
        /// been delivered (the slot ended and the dispute window has elapsed) and is not yet attached
        /// to a payout.
        /// <para>
        /// One transfer per reservation rather than one batched transfer per teacher, because the
        /// Stripe idempotency key can then be derived from the reservation id: stable forever,
        /// whatever else happens to be releasable on the next tick. A key derived from a batch is
        /// not — if the transfer lands but the local save fails, and a single entry joins or leaves
        /// the batch before the retry (another slot ends, a booking is disputed), the retry sends a
        /// different key and Stripe pays the teacher twice. It also isolates failures: an
        /// "insufficient funds" or a transient Stripe error on one lesson no longer holds back the
        /// teacher's other lessons.
        /// </para>
        /// <para>
        /// <b>Debt set-off.</b> A teacher who cancels a paid lesson owes a penalty, and a refund
        /// issued after his payout has to be clawed back. Both are booked as negative wallet lines
        /// of a DEBT type (<see cref="HardCode.DEBT_TRANSACTION_TYPES"/>) and recovered here, out of
        /// his next payouts — his wallet is very often empty when the debt is levied, so there is
        /// nothing to take at that moment. Debt is applied oldest-first, across reservations: this
        /// is the one place where a teacher's lines are looked at as a whole rather than
        /// reservation by reservation.
        /// </para>
        /// <para>
        /// <b>Why the payout row is frozen before the Stripe call.</b> The transferred amount is now
        /// net of a debt that can change between two ticks, while the idempotency key stays
        /// <c>payout_{reservationId}</c> — and Stripe rejects a replayed key whose parameters
        /// differ. So the payout (PENDING) and the PayoutId stamps are committed FIRST, and only
        /// then is the transfer requested; a row left PENDING by a crash or a Stripe failure is
        /// replayed verbatim on the next tick by <see cref="RetryPendingPayouts"/>, same key, same
        /// amount. Never recompute the amount of a PENDING payout.
        /// </para>
        /// <para>
        /// Wallet credits are created upstream when the order is paid (see
        /// <see cref="CheckoutWebhookService.HandleCompletedAsync"/>), not here.
        /// </para>
        /// </summary>
        public async Task PayoutTeacherBalances()
        {
            // Passe 0 : rejouer les transferts figés en base mais jamais confirmés par Stripe.
            // Avant toute nouvelle sélection — leurs lignes sont déjà tamponnées, donc invisibles
            // ci-dessous, et rejouer d'abord évite qu'un transfert en souffrance traîne un tick de
            // plus derrière un lot de nouveaux versements.
            await RetryPendingPayouts();

            // Passe 1 : le temps rend versable. Ici plutôt que dans une tâche séparée, pour
            // qu'une réservation devenue PAYABLE soit versée dans le même cycle.
            await MarkReservationsPayable();

            var now = DateTimeOffset.UtcNow;

            // Released = reservation PAYABLE (set by time in MarkReservationsPayable, or by a
            // dispute settled for the teacher). Unsettled = not yet part of a payout. Selecting
            // on the one state that means "payable" — not on a list of excluded states — keeps a
            // cancelled, rejected, disputed or not-yet-due booking out by construction.
            //
            // Les lignes de DETTE sont exclues d'emblée : elles relèvent de la compensation
            // (openDebts ci-dessous), pas d'un versement. Sans cette exclusion, un CLAWBACK —
            // dont la réservation est DONE avec un créneau passé — tomberait dans les DEUX
            // ensembles et serait compté deux fois. Les deux sont ainsi disjoints par
            // construction, pas par coïncidence de statut.
            var releasableEntries = await _context
                .TeacherWalletTransactions.Include(t => t.Reservation!)
                .ThenInclude(r => r.Slot)
                .Where(t =>
                    t.PayoutId == null
                    && t.ArchivedAt == null
                    && !HardCode.DEBT_TRANSACTION_TYPES.Contains(t.TypeId)
                    && t.Reservation != null
                    && t.Reservation.ArchivedAt == null
                    && t.Reservation.StatusId == HardCode.RESERVATION_PAYABLE
                    && t.Reservation.Slot != null
                )
                .ToListAsync();

            if (releasableEntries.Count == 0)
                return;

            // Les profs concernés en une requête (pas de N+1 dans la boucle ci-dessous).
            var teacherIds = releasableEntries.Select(e => e.TeacherId).Distinct().ToList();
            var teachers = await _context
                .Teachers.Where(t => teacherIds.Contains(t.Id) && t.ArchivedAt == null)
                .ToDictionaryAsync(t => t.Id);

            // Les dettes ouvertes de ces profs. PayoutId == null = pas encore recouvrée.
            // Restreint aux types de dette : une ligne REFUND est adossée à son crédit PAYMENT
            // et les deux s'annulent — la prendre pour une dette ferait payer deux fois le
            // remboursement au prof (cf. HardCode.DEBT_TRANSACTION_TYPES).
            var openDebts = await _context
                .TeacherWalletTransactions.Where(t =>
                    t.PayoutId == null
                    && t.ArchivedAt == null
                    && teacherIds.Contains(t.TeacherId)
                    && HardCode.DEBT_TRANSACTION_TYPES.Contains(t.TypeId)
                )
                .OrderBy(t => t.CreatedAt) // FIFO : la dette la plus ancienne se solde en premier
                .ToListAsync();

            var debtsByTeacher = openDebts
                .GroupBy(d => d.TeacherId)
                .ToDictionary(g => g.Key, g => new Queue<TeacherWalletTransaction>(g));

            var paidReservations = 0;

            // Par prof, puis par réservation (la plus ancienne d'abord) : le regroupement par prof
            // n'existe QUE pour imputer sa dette sur plusieurs versements — chaque réservation
            // reste un transfert distinct, avec sa propre clé d'idempotence.
            foreach (var teacherGroup in releasableEntries.GroupBy(e => e.TeacherId))
            {
                var teacherId = teacherGroup.Key;

                // Can't pay a teacher with no connected account; leave the balance to accrue.
                // Sa dette reste ouverte elle aussi : rien à compenser tant que rien ne sort.
                if (
                    !teachers.TryGetValue(teacherId, out var teacher)
                    || string.IsNullOrEmpty(teacher.StripeAccount)
                )
                    continue;

                debtsByTeacher.TryGetValue(teacherId, out var debts);

                var reservationGroups = teacherGroup
                    .GroupBy(e => e.ReservationId!.Value)
                    .OrderBy(g => g.First().Reservation!.Slot!.DateTo);

                foreach (var group in reservationGroups)
                {
                    var reservationId = group.Key;
                    var entries = group.ToList();

                    var gross = entries.Sum(e => e.Amount);
                    if (gross <= 0m)
                        continue;
                    gross = decimal.Round(gross, 2, MidpointRounding.AwayFromZero);

                    // Compensation : on solde autant de dette que ce versement peut absorber.
                    // Mute déjà les lignes de dette (tamponnage / scission) — committé au freeze
                    // ci-dessous, dans la même transaction que le versement qui les éteint.
                    var payoutId = Guid.NewGuid();
                    var debtApplied = ApplyDebt(debts, gross, payoutId, now);
                    var net = gross - debtApplied;

                    // FREEZE : le versement et le tamponnage des lignes sont écrits AVANT Stripe.
                    // Passé ce SaveChanges, `net` est gravé : un rejeu renverra ce montant-là avec
                    // la clé payout_{reservationId}, jamais un montant recalculé (Stripe rejetterait
                    // la clé, et une dette entrée entre-temps changerait le montant).
                    var payout = new TeacherPayout
                    {
                        Id = payoutId,
                        TeacherId = teacherId,
                        ReservationId = reservationId,
                        Amount = net,
                        DebtApplied = debtApplied,
                        PaidAt = now,
                        StatusId = HardCode.STATUS_TRANSACTION_PENDING,
                    };
                    _context.TeacherPayouts.Add(payout);

                    foreach (var entry in entries)
                    {
                        entry.PayoutId = payoutId;
                    }

                    // Ligne de débit visible dans l'historique du prof : un versement draine le
                    // wallet, stockée négative pour garder « somme des lignes = solde ». Rattachée
                    // au payout et volontairement SANS ReservationId : la compta d'une commande
                    // (OrderAccountingService.LoadWalletLinesAsync) ne retient que les lignes rattachées à une
                    // réservation, et compterait sinon le débit en double du crédit. Du montant NET :
                    // la part absorbée par la dette est déjà sortie du solde en tamponnant les
                    // lignes de dette.
                    if (net > 0m)
                    {
                        _context.TeacherWalletTransactions.Add(
                            new TeacherWalletTransaction
                            {
                                Id = Guid.NewGuid(),
                                Amount = -net,
                                TypeId = HardCode.TYPE_TEACHER_TRANSACTION_PAYOUT,
                                TeacherId = teacherId,
                                Payout = payout,
                                CreatedAt = now,
                            }
                        );
                    }

                    await _context.SaveChangesAsync();

                    if (await SendPayoutAsync(payout, teacher.StripeAccount!))
                        paidReservations++;
                }
            }

            Console.WriteLine($"PayoutTeacherBalances settled {paidReservations} reservation(s)");
        }

        /// <summary>
        /// Rend versables, par le temps, les réservations payées dont le créneau est terminé
        /// depuis <see cref="HardCode.DISPUTE_WINDOW"/> : <c>ACCEPTED</c> / <c>DELIVERED</c> →
        /// <c>PAYABLE</c>. Le code de présence n'est pas requis — son absence ne bloque jamais le
        /// versement. Une réservation en litige n'est pas concernée (elle est <c>DISPUTED</c>), ni
        /// le flux sans paiement, qui n'a rien à verser.
        /// </summary>
        public async Task MarkReservationsPayable()
        {
            var dueBefore = DateTimeOffset.UtcNow - HardCode.DISPUTE_WINDOW;

            var reservations = await _context
                .Reservations.Include(r => r.Order)
                .Where(r =>
                    (
                        r.StatusId == HardCode.RESERVATION_ACCEPTED
                        || r.StatusId == HardCode.RESERVATION_DELIVERED
                    )
                    && r.ArchivedAt == null
                    && r.Order != null
                    && r.Order.PaymentRequired
                    && r.Slot != null
                    && r.Slot.DateTo < dueBefore
                )
                .ToListAsync();

            if (reservations.Count == 0)
                return;

            foreach (var reservation in reservations)
            {
                reservation.TransitionStatusTo(
                    _context,
                    HardCode.RESERVATION_PAYABLE,
                    reason: "Fenêtre de litige écoulée"
                );
            }

            await _context.SaveChangesAsync();
            Console.WriteLine($"MarkReservationsPayable released {reservations.Count} reservation(s)");
        }

        /// <summary>
        /// Recovers as much of <paramref name="debts"/> as <paramref name="available"/> allows and
        /// returns the amount set off. Debt lines fully absorbed are simply stamped with the payout.
        /// A line only PARTIALLY absorbed is <b>split</b>: the original is archived and replaced by
        /// two lines summing to it — the recovered part (stamped) and the remainder (still open).
        /// Splitting rather than mutating keeps both readings of the ledger true: the sum of live
        /// lines still shows the full debt as levied (accrual — the penalty is earned when it is
        /// imposed), while the sum of *unsettled* lines shows what is still owed.
        /// </summary>
        private decimal ApplyDebt(
            Queue<TeacherWalletTransaction>? debts,
            decimal available,
            Guid payoutId,
            DateTimeOffset now
        )
        {
            if (debts is null)
                return 0m;

            var applied = 0m;

            while (available > 0m && debts.Count > 0)
            {
                var debt = debts.Peek();
                var owed = -debt.Amount; // les dettes sont stockées négatives
                if (owed <= 0m)
                {
                    debts.Dequeue(); // ligne aberrante (0 ou positive) : on ne s'en sert pas
                    continue;
                }

                if (owed <= available)
                {
                    // Absorbée en entier.
                    debt.PayoutId = payoutId;
                    debts.Dequeue();
                    available -= owed;
                    applied += owed;
                    continue;
                }

                // Absorbée en partie : scission en deux lignes qui somment à l'originale.
                debt.ArchivedAt = now;
                debt.UpdatedAt = now;

                _context.TeacherWalletTransactions.Add(
                    new TeacherWalletTransaction
                    {
                        Id = Guid.NewGuid(),
                        Amount = -available, // recouvré
                        TypeId = debt.TypeId,
                        TeacherId = debt.TeacherId,
                        ReservationId = debt.ReservationId,
                        PayoutId = payoutId,
                        CreatedAt = now,
                    }
                );

                var remainder = new TeacherWalletTransaction
                {
                    Id = Guid.NewGuid(),
                    Amount = -(owed - available), // reste dû, recouvré au prochain versement
                    TypeId = debt.TypeId,
                    TeacherId = debt.TeacherId,
                    ReservationId = debt.ReservationId,
                    CreatedAt = now,
                };
                _context.TeacherWalletTransactions.Add(remainder);

                // Le reliquat reprend la tête de file : c'est lui que le versement suivant du
                // même prof (boucle appelante) attaquera en premier.
                debts.Dequeue();
                var rest = debts.ToList();
                debts.Clear();
                debts.Enqueue(remainder);
                foreach (var d in rest)
                    debts.Enqueue(d);

                applied += available;
                available = 0m;
            }

            return applied;
        }

        /// <summary>
        /// Sends the Stripe transfer for a payout frozen in PENDING and closes it out. Returns true
        /// when the payout is settled (transfer acknowledged, or nothing to transfer because a debt
        /// swallowed the whole credit). On failure the payout stays PENDING and is replayed as-is on
        /// the next tick — the entries stay stamped, so nothing else picks them up in the meantime.
        /// </summary>
        private async Task<bool> SendPayoutAsync(TeacherPayout payout, string stripeAccount)
        {
            var amountInCents = (long)(payout.Amount * 100);

            if (amountInCents > 0)
            {
                var response = await _connectService.CreateTransferAsync(
                    new StripeConnectService.TransferRequest(
                        DestinationAccountId: stripeAccount,
                        Amount: amountInCents,
                        Description: $"Versement réservation {payout.ReservationId}",
                        // Dérivée de la réservation, donc stable dans le temps : si le transfert
                        // part mais que le save local échoue, le tick suivant rejoue exactement la
                        // même clé ET le même montant (figé sur le payout), et Stripe (sous 24h)
                        // refuse d'envoyer l'argent deux fois.
                        IdempotencyKey: $"payout_{payout.ReservationId}"
                    )
                );

                if (response.Status != 200 || response.Data is null)
                {
                    // Échec isolé : les autres réservations du prof passent quand même, et
                    // celle-ci est rejouée au prochain tick (le payout reste PENDING). Cas
                    // typique : "insufficient funds" sur le solde plateforme, qui se résorbe
                    // avec le settlement Stripe.
                    Console.WriteLine(
                        $"PayoutTeacherBalances transfer failed for reservation {payout.ReservationId} "
                            + $"(payout {payout.Id}, retried next tick): {response.Message}"
                    );
                    return false;
                }

                payout.StripeTransferId = response.Data;
            }
            // amountInCents == 0 : la dette a absorbé tout le crédit. Le cours est bien réglé —
            // par compensation — donc pas d'appel Stripe, mais le payout existe et vaut 0.

            payout.StatusId = HardCode.STATUS_TRANSACTION_PAID;
            payout.UpdatedAt = DateTimeOffset.UtcNow;

            // Boucle de cycle de vie fermée : le cours est réglé au prof → PAYABLE → DONE, journalisé dans
            // l'historique (acteur null = système/cron). No-op si la réservation est déjà DONE,
            // donc sûr au rejeu.
            var reservation = await _context.Reservations.FirstOrDefaultAsync(r =>
                r.Id == payout.ReservationId
            );
            reservation?.TransitionStatusTo(_context, HardCode.RESERVATION_DONE, reason: "Versé au prof");

            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Replays the transfers of payouts frozen in PENDING: the DB freeze succeeded but Stripe
        /// never acknowledged (crash, timeout, insufficient funds). The amount is NOT recomputed —
        /// it is replayed exactly as frozen, with the same idempotency key, which is the whole point
        /// of freezing it. A payout stuck here for days means a real problem on the destination
        /// account (restricted / disabled), so it is logged loudly.
        /// </summary>
        private async Task RetryPendingPayouts()
        {
            var pending = await _context
                .TeacherPayouts.Include(p => p.Teacher)
                .Where(p =>
                    p.StatusId == HardCode.STATUS_TRANSACTION_PENDING
                    && p.ArchivedAt == null
                    && p.ReservationId != null
                )
                .ToListAsync();

            if (pending.Count == 0)
                return;

            var stale = DateTimeOffset.UtcNow - TimeSpan.FromDays(3);

            foreach (var payout in pending)
            {
                var stripeAccount = payout.Teacher?.StripeAccount;
                if (string.IsNullOrEmpty(stripeAccount))
                    continue;

                if (payout.PaidAt < stale)
                {
                    Console.WriteLine(
                        $"PayoutTeacherBalances: payout {payout.Id} (reservation {payout.ReservationId}, "
                            + $"{payout.Amount:0.00} €) stuck PENDING since {payout.PaidAt:u} — check the "
                            + "teacher's Stripe account"
                    );
                }

                await SendPayoutAsync(payout, stripeAccount!);
            }
        }

        /// <summary>
        /// Issues the student refunds that were scheduled at cancellation time. When a student
        /// cancels a PAID reservation the money is NOT refunded inline: a <see cref="Refund"/> row
        /// is recorded in PENDING state and the buyer is reimbursed here, 48h later — the same
        /// deferral window as the teacher payout, so a last-minute reversal never races a
        /// synchronous charge-back and the cancellation request never blocks on Stripe.
        /// <para>
        /// Selects PENDING refunds older than the 48h window, issues one Stripe refund each
        /// (idempotent via a key derived from the reservation, so a retry after a partial failure
        /// never double-refunds), stamps the Stripe refund id and flips the row to PAID. A failed
        /// refund stays PENDING and is retried on the next tick, mirroring the payout job's
        /// transient-error handling. The frozen teacher credit was already neutralised (negative
        /// wallet line) and the reservation moved to CANCELLED at cancellation time, so no money
        /// can reach the teacher in the meantime.
        /// </para>
        /// </summary>
        public async Task ProcessPendingRefunds()
        {
            var now = DateTimeOffset.UtcNow;
            // Fenêtre de refroidissement, alignée sur le délai de payout (même constante).
            var refundDate = now - HardCode.DISPUTE_WINDOW;

            var pendingRefunds = await _context
                .Refunds.Include(r => r.Payment)
                .Where(r =>
                    r.StatusId == HardCode.STATUS_TRANSACTION_PENDING
                    && r.StripeRefundId == null
                    && r.ArchivedAt == null
                    && r.CreatedAt < refundDate
                )
                .ToListAsync();

            if (pendingRefunds.Count == 0)
                return;

            var refunded = 0;

            foreach (var refund in pendingRefunds)
            {
                // The original charge's PaymentIntent is reached through the settling Payment.
                var paymentIntentId = refund.Payment?.TransactionRef;
                if (string.IsNullOrEmpty(paymentIntentId))
                {
                    Console.WriteLine(
                        $"ProcessPendingRefunds: refund {refund.Id} has no PaymentIntent; skipped"
                    );
                    continue;
                }

                var result = await _refundService.RefundReservationAsync(
                    new StripeRefundService.RefundRequest(
                        PaymentIntentId: paymentIntentId,
                        Amount: (long)(refund.Amount * 100),
                        // Recopiée en metadata Stripe : c'est ce qui permet au webhook
                        // charge.refunded de reconnaître ce remboursement comme émis par l'app.
                        ReservationId: refund.ReservationId,
                        // Derived from the reservation so a retry after a crash reuses the same key
                        // and Stripe (within 24h) refuses to refund the same booking twice.
                        IdempotencyKey: $"refund_{refund.ReservationId}"
                    )
                );

                if (result.Status != 200 || result.Data is null)
                {
                    // Leave PENDING; the next tick retries (e.g. transient Stripe error).
                    Console.WriteLine(
                        $"ProcessPendingRefunds: refund {refund.Id} failed: {result.Message}"
                    );
                    continue;
                }

                refund.StripeRefundId = result.Data;
                refund.StatusId = HardCode.STATUS_TRANSACTION_PAID;
                refund.UpdatedAt = now;

                await _context.SaveChangesAsync();
                refunded++;
            }

            Console.WriteLine($"ProcessPendingRefunds reimbursed {refunded} refund(s)");
        }

        /// <summary>
        /// RGPD : supprime du stockage le fichier des justificatifs prof devenus inutiles, en
        /// gardant la ligne (type, statut, revue) pour la vérification et la traçabilité. Sont
        /// purgés les documents remplacés ou retirés et les documents refusés depuis plus de
        /// <see cref="EnvironmentVariables.PRIVACY_DOCUMENT_RETENTION_DAYS"/> jours, ainsi que tous
        /// les documents d'un compte archivé. Un document vivant validé ou en attente est conservé.
        /// Idempotent.
        /// </summary>
        public async Task PurgeExpiredPrivacyDocuments()
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(
                -EnvironmentVariables.PRIVACY_DOCUMENT_RETENTION_DAYS
            );

            var expired = await _context
                .PrivacyDocuments.Where(d =>
                    d.FilePurgedAt == null
                    && (
                        d.ArchivedAt < cutoff
                        || (d.Status == DocumentReviewStatusEnum.Refused && d.ReviewedAt < cutoff)
                        || d.Teacher!.User!.ArchivedAt != null
                    )
                )
                .ToListAsync();

            if (expired.Count == 0)
                return;

            var now = DateTimeOffset.UtcNow;
            foreach (var document in expired)
            {
                await _storage.Delete(document.FilePath, CancellationToken.None);
                document.FilePurgedAt = now;
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Expires a Stripe checkout session so its payment link can no longer be used.
        /// Returns null on any failure (e.g. the session is already expired/completed).
        /// </summary>
        public async Task<Stripe.Checkout.Session?> ExpireCheckout(string checkoutId)
        {
            try
            {
                if (string.IsNullOrEmpty(checkoutId))
                {
                    return null;
                }

                StripeConfiguration.ApiKey = EnvironmentVariables.STRIPE_SK;
                var service = new Stripe.Checkout.SessionService();
                return await service.ExpireAsync(checkoutId);
            }
            catch
            {
                return null;
            }
        }
    }
}
