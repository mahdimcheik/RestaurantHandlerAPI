using System.Globalization;
using System.Text.Json;
using RestaurantHandler.Contexts;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

/// <summary>
/// Perspective de lecture pour le listing unifié des litiges — pilote le scope de base et
/// quels filtres de nom (prof / élève) s'appliquent.
/// </summary>
public enum DisputeViewRole
{
    Student,
    Teacher,
    Admin,
}

/// <summary>
/// CRUD des litiges (Feature 8). Le litige est le seul mécanisme qui bloque le versement du
/// prof : sa GARDE de payout reste portée par <see cref="Reservation.StatusId"/> == DISPUTED
/// (exclue du cron). Ce service porte le CONTENU du dossier — ouverture, listing, détail.
/// </summary>
public class DisputeService(
    MainContext context,
    NotificationDispatcher notifications,
    StorageService storage
)
{
    /// <summary>Liste des statuts possibles d'un litige (Open/Resolved), pour peupler un filtre.</summary>
    public async Task<Response<List<DisputeStatusDetails>>> GetAllDisputeStatusesAsync()
    {
        var statuses = await context
            .DisputeStatuses.AsNoTracking()
            .Where(s => s.ArchivedAt == null)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new DisputeStatusDetails(s))
            .ToListAsync();

        return new Response<List<DisputeStatusDetails>>
        {
            Status = 200,
            Message = "Statuts de litige récupérés avec succès",
            Data = statuses,
            Count = statuses.Count,
        };
    }

    /// <summary>Liste des issues possibles d'un litige résolu (Teacher/Student/Partial), pour peupler un filtre.</summary>
    public async Task<Response<List<DisputeOutcomeDetails>>> GetAllDisputeOutcomesAsync()
    {
        var outcomes = await context
            .DisputeOutcomes.AsNoTracking()
            .Where(o => o.ArchivedAt == null)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new DisputeOutcomeDetails(o))
            .ToListAsync();

        return new Response<List<DisputeOutcomeDetails>>
        {
            Status = 200,
            Message = "Issues de litige récupérées avec succès",
            Data = outcomes,
            Count = outcomes.Count,
        };
    }

    public Task<Response<List<DisputeDetails>>> GetDisputesByStudentPaginated(
        CustomTableState query,
        CallerIdentity? principals
    ) => GetDisputesPaginated(query, principals, DisputeViewRole.Student);

    public Task<Response<List<DisputeDetails>>> GetDisputesByTeacherPaginated(
        CustomTableState query,
        CallerIdentity? principals
    ) => GetDisputesPaginated(query, principals, DisputeViewRole.Teacher);

    public Task<Response<List<DisputeDetails>>> GetDisputesByAdminPaginated(
        CustomTableState query
    ) => GetDisputesPaginated(query, null, DisputeViewRole.Admin);

    /// <summary>
    /// Listing paginé unifié partagé par les endpoints élève, prof et admin. Le
    /// <paramref name="role"/> pilote le scope de base et les filtres de nom applicables :
    /// <list type="bullet">
    /// <item>Élève — les litiges sur ses propres réservations, filtrable par nom de prof.</item>
    /// <item>Prof — les litiges sur les réservations de ses créneaux, filtrable par nom d'élève.</item>
    /// <item>Admin — tous les litiges, filtrable par les deux.</item>
    /// </list>
    /// Filtres communs (statut, issue, recherche sur le motif, plage de dates d'ouverture).
    /// </summary>
    private async Task<Response<List<DisputeDetails>>> GetDisputesPaginated(
        CustomTableState query,
        CallerIdentity? principals,
        DisputeViewRole role
    )
    {
        IQueryable<Dispute> queryable;

        if (role == DisputeViewRole.Admin)
        {
            queryable = context.Disputes.Where(d => d.ArchivedAt == null);
        }
        else
        {
            var user = CheckUser.GetUser(principals, context);

            if (user is null)
            {
                return new Response<List<DisputeDetails>>
                {
                    Status = 404,
                    Message = "Utilisateur non trouvé",
                    Data = null,
                };
            }

            queryable =
                role == DisputeViewRole.Teacher
                    ? context.Disputes.Where(d =>
                        d.Reservation.Slot.TeacherId == user.Id && d.ArchivedAt == null
                    )
                    : context.Disputes.Where(d =>
                        d.Reservation.StudentId == user.Id && d.ArchivedAt == null
                    );
        }

        IQueryable<Dispute> disputes = queryable
            .Include(d => d.OpenedBy)
            .Include(d => d.ResolvedBy)
            .Include(d => d.Status)
            .Include(d => d.Outcome)
            .Include(d => d.Reservation)
            .ThenInclude(r => r.Slot)
            .ThenInclude(s => s.Teacher)
            .ThenInclude(t => t.User)
            .Include(d => d.Reservation)
            .ThenInclude(r => r.Student)
            .ThenInclude(s => s.User)
            .Include(d => d.Reservation)
            .ThenInclude(r => r.Status);

        // teacher name filter — student & admin views
        if (
            role != DisputeViewRole.Teacher
            && query.Filters?.TryGetValue("teacherName", out Filter teacherName) == true
        )
        {
            disputes = disputes.Where(d =>
                EF.Functions.ILike(
                    d.Reservation.Slot.Teacher.User.FirstName.ToLower()
                        + " "
                        + d.Reservation.Slot.Teacher.User.LastName.ToLower(),
                    $"%{teacherName.Value.ToString().Trim()}%"
                )
            );
        }

        // student name filter — teacher & admin views
        if (
            role != DisputeViewRole.Student
            && query.Filters?.TryGetValue("studentName", out Filter studentName) == true
        )
        {
            disputes = disputes.Where(d =>
                EF.Functions.ILike(
                    d.Reservation.Student.User.FirstName.ToLower()
                        + " "
                        + d.Reservation.Student.User.LastName.ToLower(),
                    $"%{studentName.Value.ToString().Trim()}%"
                )
            );
        }

        // status filter (Dispute.StatusId, GUID — cf. HardCode.DISPUTE_STATUS_*)
        if (
            query.Filters?.TryGetValue("status", out Filter? statuses) == true
            && statuses != null
        )
        {
            var ids = JsonSerializer.Deserialize<Guid[]>(statuses.Value?.ToString() ?? "[]");
            if (ids != null && ids.Length > 0)
                disputes = disputes.Where(d => ids.Contains(d.StatusId));
        }

        // outcome filter (Dispute.OutcomeId, GUID — cf. HardCode.DISPUTE_OUTCOME_*)
        if (
            query.Filters?.TryGetValue("outcome", out Filter? outcomes) == true
            && outcomes != null
        )
        {
            var ids = JsonSerializer.Deserialize<Guid[]>(outcomes.Value?.ToString() ?? "[]");
            if (ids != null && ids.Length > 0)
                disputes = disputes.Where(d =>
                    d.OutcomeId != null && ids.Contains(d.OutcomeId.Value)
                );
        }

        // dispute opening date range
        if (
            query.Filters?.TryGetValue("dateFrom", out Filter dateFromFilter) == true
            && DateTimeOffset.TryParse(
                dateFromFilter.Value?.ToString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dateFrom
            )
        )
        {
            disputes = disputes.Where(d => d.CreatedAt > dateFrom);
        }

        if (
            query.Filters?.TryGetValue("dateTo", out Filter dateToFilter) == true
            && DateTimeOffset.TryParse(
                dateToFilter.Value?.ToString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dateTo
            )
        )
        {
            disputes = disputes.Where(d => d.CreatedAt < dateTo);
        }

        // global filter — sur le motif
        if (!string.IsNullOrEmpty(query.Search?.Trim()))
        {
            var trimmed = query.Search.Trim();
            disputes = disputes.Where(d => EF.Functions.ILike(d.Reason, $"%{trimmed}%"));
        }

        var count = await disputes.CountAsync();

        if (query.Rows <= 0)
        {
            query.Rows = 10;
        }

        var results = await disputes
            .OrderByDescending(d => d.CreatedAt)
            .ThenBy(d => d.Id)
            .Skip(query.First)
            .Take(query.Rows)
            .ToListAsync();

        await HydrateAvatarsAsync(results, role);

        return new Response<List<DisputeDetails>>
        {
            Status = 200,
            Message = "Litiges récupérés avec succès",
            Data = results.Select(d => new DisputeDetails(d)).ToList(),
            Count = count,
        };
    }

    /// <summary>
    /// Détail d'un litige avec sa réservation (prof, élève, statut, historique) et l'admin/prof
    /// qui l'a éventuellement tranché. Un élève / prof ne peut consulter que ses propres litiges ;
    /// l'admin voit tout.
    /// </summary>
    public async Task<Response<DisputeDetails>> GetDisputeById(
        Guid disputeId,
        CallerIdentity? principals
    )
    {
        var user = CheckUser.GetUser(principals, context);
        if (user is null)
        {
            return new Response<DisputeDetails>
            {
                Status = 404,
                Message = "Utilisateur non trouvé",
                Data = null,
            };
        }

        var isAdmin = principals?.IsAdmin == true;

        var dispute = await context
            .Disputes.Where(d => d.Id == disputeId && d.ArchivedAt == null)
            .Include(d => d.OpenedBy)
            .Include(d => d.ResolvedBy)
            .Include(d => d.Status)
            .Include(d => d.Outcome)
            .Include(d => d.Reservation)
            .ThenInclude(r => r.Slot)
            .ThenInclude(s => s.Teacher)
            .ThenInclude(t => t.User)
            .Include(d => d.Reservation)
            .ThenInclude(r => r.Student)
            .ThenInclude(s => s.User)
            .Include(d => d.Reservation)
            .ThenInclude(r => r.Status)
            .Include(d => d.Reservation)
            .ThenInclude(r => r.StatusHistory)
            .FirstOrDefaultAsync();

        if (dispute is null)
        {
            return new Response<DisputeDetails>
            {
                Status = 404,
                Message = "Litige non trouvé",
                Data = null,
            };
        }

        // Un élève ne voit que les litiges sur ses réservations ; un prof que ceux sur ses
        // créneaux. L'admin n'est pas restreint.
        var isOwner =
            dispute.Reservation?.StudentId == user.Id
            || dispute.Reservation?.Slot?.TeacherId == user.Id;
        if (!isAdmin && !isOwner)
        {
            return new Response<DisputeDetails>
            {
                Status = 404,
                Message = "Litige non trouvé",
                Data = null,
            };
        }

        HydrateAvatar(dispute.Reservation?.Slot?.Teacher?.User);
        HydrateAvatar(dispute.Reservation?.Student?.User);

        return new Response<DisputeDetails>
        {
            Status = 200,
            Message = "Litige récupéré avec succès",
            Data = new DisputeDetails(dispute),
        };
    }

    /// <summary>
    /// L'élève ouvre un litige sur une réservation payée dont le cours est passé. Le seul
    /// effet est de passer la réservation en <c>DISPUTED</c> : cela suffit à geler le crédit
    /// du prof, car le cron de payout exclut ce statut. Le prof ET les admins sont notifiés
    /// simultanément (Feature 8) pour l'arbitrage ultérieur.
    ///
    /// Réservé au flux payant : sans paiement, il n'y a ni crédit à geler ni encaissement à
    /// rembourser (ADR-0010).
    ///
    /// Autorisé uniquement entre la fin du créneau et l'échéance de payout
    /// (<see cref="HardCode.DISPUTE_WINDOW"/>) : avant le cours c'est une annulation, après
    /// la fenêtre le crédit est déjà/va être versé (trop tard, pas de clawback à gérer).
    /// </summary>
    public async Task OpenDisputeAsync(DisputeCreate dto, CallerIdentity? principals)
    {
        var student = CheckUser.GetUser(principals, context);
        if (student is null)
        {
            throw new ResourceNotFoundException("Utilisateur non trouvé");
        }

        // Seule une réservation PAYÉE (ACCEPTED / DELIVERED) est contestable : PENDING = panier
        // non payé, PAYABLE / DONE = trop tard, DISPUTED = litige déjà ouvert. Le filtre StudentId
        // garantit qu'un élève ne conteste que ses propres réservations.
        var reservation = await context
            .Reservations.Where(r =>
                r.Id == dto.ReservationId
                && r.ArchivedAt == null
                && r.StudentId == student.Id
                && r.Order != null
                && r.Order.PaymentRequired
                && (
                    r.StatusId == HardCode.RESERVATION_ACCEPTED
                    || r.StatusId == HardCode.RESERVATION_DELIVERED
                )
            )
            .Include(r => r.Slot)
            .ThenInclude(s => s.Teacher.User)
            .FirstOrDefaultAsync();

        if (reservation is null)
        {
            throw new ResourceNotFoundException("Réservation non trouvée");
        }

        var now = DateTimeOffset.UtcNow;
        if (reservation.Slot.DateTo > now)
        {
            throw new BusinessConflictException("Le cours n'a pas encore eu lieu");
        }
        if (reservation.Slot.DateTo < now - HardCode.DISPUTE_WINDOW)
        {
            throw new BusinessConflictException("Délai de litige dépassé");
        }

        // Le passage en DISPUTED gèle le crédit (le cron de payout filtre ce statut) et
        // journalise le motif dans l'historique. Aucun mouvement d'argent ici.
        reservation.TransitionStatusTo(
            context,
            HardCode.RESERVATION_DISPUTED,
            student.Id,
            dto.Reason
        );

        // Dossier du litige : contenu (motif, ouvreur, cycle de vie, résolution à venir).
        // La garde de payout reste portée par le statut DISPUTED ci-dessus.
        var dispute = new Dispute
        {
            ReservationId = reservation.Id,
            OpenedById = student.Id,
            Reason = dto.Reason,
            StatusId = HardCode.DISPUTE_STATUS_OPEN,
        };
        context.Disputes.Add(dispute);
        await context.SaveChangesAsync();

        // Notifie le prof ET les admins en même temps (Feature 8).
        const string openedMessage = "Un litige a été ouvert sur une réservation";
        var recipients = new List<NotificationRequest>
        {
            new(
                reservation.Slot.TeacherId,
                reservation.Slot.Teacher.User.Email,
                NotificationTypeEnum.Reservation,
                openedMessage,
                "#ff3b30",
                "pi pi-exclamation-triangle",
                NotificationLinks.Dispute(dispute.Id, forTeacher: true)
            ),
        };
        foreach (var admin in await notifications.GetAdminRecipientsAsync())
        {
            recipients.Add(
                new(
                    admin.UserId,
                    admin.Email,
                    NotificationTypeEnum.Reservation,
                    openedMessage,
                    "#ff3b30",
                    "pi pi-exclamation-triangle",
                    NotificationLinks.AdminDispute(dispute.Id, admin.IsSuperAdmin)
                )
            );
        }
        await notifications.NotifyAsync([.. recipients]);
    }

    /// <summary>
    /// L'admin résout un litige ouvert, paramétré par <see cref="DisputeClose.RefundAmount"/>
    /// (R, montant brut remboursé à l'élève) :
    /// <list type="bullet">
    /// <item><c>R == 0</c> → pro-prof : réservation <c>DISPUTED → PAYABLE</c>, le crédit gelé
    /// est dégelé et redevient payable (issue <see cref="HardCode.DISPUTE_OUTCOME_TEACHER"/>).</item>
    /// <item><c>R == Amount</c> → pro-élève : Refund PENDING, crédit prof neutralisé,
    /// réservation <c>CANCELLED</c> (issue <see cref="HardCode.DISPUTE_OUTCOME_STUDENT"/>).</item>
    /// <item><c>0 &lt; R &lt; Amount</c> → partiel : Refund de R + ligne wallet
    /// <c>-min(R, part prof)</c>, réservation <c>PAYABLE</c> pour verser le reste (issue
    /// <see cref="HardCode.DISPUTE_OUTCOME_PARTIAL"/>).</item>
    /// </list>
    /// Le calcul est celui de <see cref="DisputeResolutionPolicy"/> : le remboursement est à la
    /// charge du prof, la plateforme garde sa commission. Le remboursement Stripe est différé
    /// (cron <c>ProcessPendingRefunds</c>). L'élève et le prof sont notifiés de l'issue.
    /// </summary>
    public async Task CloseDisputeAsync(DisputeClose dto, CallerIdentity? principals)
    {
        var admin = CheckUser.GetUser(principals, context);
        if (admin is null)
        {
            throw new ResourceNotFoundException("Utilisateur non trouvé");
        }

        var dispute = await context
            .Disputes.Where(d => d.Id == dto.DisputeId && d.ArchivedAt == null)
            .Include(d => d.Reservation)
            .ThenInclude(r => r.Slot)
            .ThenInclude(s => s.Teacher.User)
            .Include(d => d.Reservation)
            .ThenInclude(r => r.Student.User)
            .Include(d => d.Reservation)
            .ThenInclude(r => r.Order)
            .ThenInclude(o => o!.Payment)
            .Include(d => d.Reservation)
            .ThenInclude(r => r.Order)
            .ThenInclude(o => o!.Reservations)
            .FirstOrDefaultAsync();

        if (dispute is null)
        {
            throw new ResourceNotFoundException("Litige non trouvé");
        }
        if (dispute.StatusId != HardCode.DISPUTE_STATUS_OPEN)
        {
            throw new BusinessConflictException("Le litige est déjà clôturé");
        }
        if (dispute.Reservation is null)
        {
            throw new ResourceNotFoundException("Réservation non trouvée");
        }

        var reservation = dispute.Reservation;

        var resolution = DisputeResolutionPolicy.Resolve(reservation, dto.RefundAmount);
        if (resolution is null)
        {
            throw new BusinessRuleException(
                "Le montant remboursé doit être compris entre 0 et le montant de la réservation"
            );
        }

        var (outcomeId, refundAmount, teacherReversal, reservationStatusId) = resolution.Value;

        // Côté remboursement (uniquement si R > 0) : on rejoue la mécanique d'annulation —
        // ligne Refund PENDING (le cron ProcessPendingRefunds émet le refund Stripe 48h plus
        // tard) + ligne wallet REFUND négative qui neutralise la part gelée du crédit prof.
        // Aucun clawback : le crédit est forcément encore PayoutId == null (DISPUTED = gelé).
        if (refundAmount > 0m)
        {
            var order = reservation.Order;
            if (order?.Payment?.TransactionRef is null)
            {
                throw new InvalidOperationException("Paiement introuvable : remboursement impossible");
            }

            context.Refunds.Add(
                new Refund
                {
                    Amount = refundAmount,
                    ReservationId = reservation.Id,
                    OrderId = order.Id,
                    PaymentId = order.Payment.Id,
                    StripeRefundId = null,
                    Reason = dto.ResolutionNote ?? "Litige tranché en faveur de l'élève",
                    StatusId = HardCode.STATUS_TRANSACTION_PENDING,
                }
            );

            if (teacherReversal > 0m)
            {
                context.TeacherWalletTransactions.Add(
                    new TeacherWalletTransaction
                    {
                        Amount = -teacherReversal,
                        TypeId = HardCode.TYPE_TEACHER_TRANSACTION_REFUND,
                        TeacherId = reservation.Slot.TeacherId,
                        ReservationId = reservation.Id,
                    }
                );
            }
        }

        // Sortie de DISPUTED. Remboursement total → CANCELLED (crédit + reversal s'annulent,
        // tous deux exclus du payout). Sinon (payout total OU partiel) → PAYABLE, jamais DONE :
        // DONE signifie « professeur versé » et seule la tâche de versement peut l'affirmer. Le
        // crédit restant est dégelé et le cron somme +P(1-c) et -min(R, P(1-c)).
        reservation.TransitionStatusTo(
            context,
            reservationStatusId,
            admin.Id,
            dto.ResolutionNote ?? "Litige résolu par l'admin"
        );

        // Statut de la commande (jamais le total) uniquement si un remboursement a lieu :
        // REFUNDED si toutes ses lignes sont annulées, sinon REFUNDED_PARTIALLY.
        if (refundAmount > 0m && reservation.Order is not null)
        {
            var order = reservation.Order;
            var allCancelled = order.Reservations.All(r =>
                r.StatusId == HardCode.RESERVATION_CANCELLED
            );
            order.StatusId = allCancelled
                ? HardCode.STATUS_ORDER_REFUNDED
                : HardCode.STATUS_ORDER_REFUNDED_PARTIALLY;
            order.UpdatedAt = DateTimeOffset.UtcNow;
        }

        dispute.StatusId = HardCode.DISPUTE_STATUS_RESOLVED;
        dispute.OutcomeId = outcomeId;
        dispute.ResolvedById = admin.Id;
        dispute.ResolvedAt = DateTimeOffset.UtcNow;
        dispute.ResolutionNote = dto.ResolutionNote;

        await context.SaveChangesAsync();

        // Notifie l'élève (ouvreur) ET le prof que le litige est résolu, sur le pattern
        // inline habituel (Notification persistée + push SignalR par email). Le libellé
        // reflète l'issue (remboursement total / partiel / clôture pro-prof).
        var outcomeMessage = dispute.OutcomeId switch
        {
            var id when id == HardCode.DISPUTE_OUTCOME_STUDENT => "Un litige a été tranché : remboursement de l'élève",
            var id when id == HardCode.DISPUTE_OUTCOME_PARTIAL => "Un litige a été tranché : remboursement partiel",
            _ => "Un litige a été clôturé en faveur du prof",
        };

        await notifications.NotifyAsync(
            new NotificationRequest(
                dispute.Reservation.StudentId,
                dispute.Reservation.Student?.User?.Email,
                NotificationTypeEnum.Reservation,
                outcomeMessage,
                "#34c759",
                "pi pi-check-circle",
                NotificationLinks.Dispute(dispute.Id, forTeacher: false)
            ),
            new NotificationRequest(
                dispute.Reservation.Slot.TeacherId,
                dispute.Reservation.Slot.Teacher?.User?.Email,
                NotificationTypeEnum.Reservation,
                outcomeMessage,
                "#34c759",
                "pi pi-check-circle",
                NotificationLinks.Dispute(dispute.Id, forTeacher: true)
            )
        );
    }

    /// <summary>
    /// Remplace les clés objet des avatars (prof côté élève/admin, élève côté prof/admin) par
    /// leur URL publique. Best-effort : un échec ne casse pas le listing.
    /// </summary>
    private async Task HydrateAvatarsAsync(List<Dispute> disputes, DisputeViewRole role)
    {
        if (role != DisputeViewRole.Student)
        {
            foreach (
                var user in disputes
                    .Select(d => d.Reservation?.Student?.User)
                    .Where(u => u is not null)
                    .DistinctBy(u => u!.Id)
            )
            {
                HydrateAvatar(user);
            }
        }

        if (role != DisputeViewRole.Teacher)
        {
            foreach (
                var user in disputes
                    .Select(d => d.Reservation?.Slot?.Teacher?.User)
                    .Where(u => u is not null)
                    .DistinctBy(u => u!.Id)
            )
            {
                HydrateAvatar(user);
            }
        }
    }

    private void HydrateAvatar(UserApp? user)
    {
        if (!string.IsNullOrEmpty(user?.ImgUrl))
        {
            user.ImgUrl = storage.Url(user.ImgUrl);
        }
    }
}
