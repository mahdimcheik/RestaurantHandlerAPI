using System.Text.Json;
using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

public class TeacherWalletService(MainContext context)
{
    /// <summary>
    /// Le portefeuille d'un professeur n'est lisible que par lui et par l'administration ; pour
    /// tout autre appelant, il n'existe pas.
    /// </summary>
    private Task<bool> CanReadWalletAsync(Guid teacherId, CallerIdentity caller) =>
        caller.IsAdmin
            ? Task.FromResult(true)
            : context.Teachers.AnyAsync(t => t.Id == teacherId && t.UserId == caller.UserId);

    /// <summary>
    /// Paginated list of a teacher's wallet transactions (payouts/earnings/refunds),
    /// filterable by reservation number (the related order number) and by transaction type.
    /// </summary>
    public async Task<Response<List<TeacherWalletTransactionDetails>>> GetTransactionsByTeacherAsync(
        Guid teacherId,
        CustomTableState query,
        CallerIdentity caller
    )
    {
        if (!await CanReadWalletAsync(teacherId, caller))
            return new Response<List<TeacherWalletTransactionDetails>>
            {
                Status = 404,
                Message = "Portefeuille introuvable",
            };

        IQueryable<TeacherWalletTransaction> transactions = context.TeacherWalletTransactions
            .Where(t => t.TeacherId == teacherId && t.ArchivedAt == null);

        if (query.Filters?.TryGetValue("type", out Filter? type) == true && type != null)
        {
            var ids = JsonSerializer.Deserialize<Guid[]>(type.Value?.ToString() ?? "[]");
            if (ids != null && ids.Length > 0)
                transactions = transactions.Where(t => ids.Contains(t.TypeId));
        }

        if (query.Filters?.TryGetValue("reservationNumber", out Filter? reservationNumber) == true
            && reservationNumber?.Value != null)
        {
            var trimmed = reservationNumber.Value.ToString()!.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                transactions = transactions.Where(t =>
                    t.Reservation != null
                    && t.Reservation.Order != null
                    && EF.Functions.ILike(t.Reservation.Order.OrderNumber, $"%{trimmed}%"));
        }

        var count = await transactions.CountAsync();

        if (query.Rows <= 0)
            query.Rows = 10;

        var payoutTypeName = TeacherTransactionTypeEnum.Payout.ToString();

        // Projected column by column rather than through the TeacherWalletTransactionDetails
        // constructor: passing the entity would load every column of the 7 joined tables,
        // including both AspNetUsers rows (password hash, security stamp…).
        var data = await transactions
            .OrderByDescending(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .Skip(query.First)
            .Take(query.Rows)
            .Select(t => new TeacherWalletTransactionDetails
            {
                Id = t.Id,
                Amount = t.Amount,
                CreatedAt = t.CreatedAt,
                Type = t.Type == null
                    ? null
                    : new TypeTeacherTransactionDetails
                    {
                        Id = t.Type.Id,
                        Name = t.Type.Name,
                        Color = t.Type.Color,
                        Icon = t.Type.Icon,
                        Translations = t.Type.Translations,
                    },
                ReservationId = t.ReservationId,
                ReservationTitle = t.Reservation != null ? t.Reservation.Title : null,
                ReservationNumber = t.Reservation != null && t.Reservation.Order != null
                    ? t.Reservation.Order.OrderNumber
                    : null,
                CounterpartyName = t.Type != null && t.Type.Name == payoutTypeName
                    ? (t.Teacher != null && t.Teacher.User != null
                        ? (t.Teacher.User.FirstName + " " + t.Teacher.User.LastName).Trim()
                        : null)
                    : (t.Reservation != null && t.Reservation.Student != null && t.Reservation.Student.User != null
                        ? (t.Reservation.Student.User.FirstName + " " + t.Reservation.Student.User.LastName).Trim()
                        : null),
            })
            .ToListAsync();

        return new Response<List<TeacherWalletTransactionDetails>>
        {
            Status = 200,
            Message = "Transactions récupérées avec succès",
            Data = data,
            Count = count,
        };
    }

    /// <summary>
    /// Solde d'un prof, dérivé du ledger : somme de ses lignes non réglées, séparée en crédits
    /// (ce qu'on lui doit) et dettes (ce qu'il doit — pénalités d'annulation, clawbacks).
    /// Une seule requête, agrégée en base.
    /// </summary>
    public async Task<Response<TeacherWalletBalance>> GetBalanceAsync(
        Guid teacherId,
        CallerIdentity caller
    )
    {
        if (!await CanReadWalletAsync(teacherId, caller))
            return new Response<TeacherWalletBalance>
            {
                Status = 404,
                Message = "Portefeuille introuvable",
            };

        // PayoutId == null = pas encore réglé, dans un sens comme dans l'autre. Les lignes
        // déjà rattachées à un payout (crédits versés, dettes recouvrées, débit PAYOUT) sont
        // soldées et sortent du solde par construction.
        var open = await context
            .TeacherWalletTransactions.Where(t =>
                t.TeacherId == teacherId && t.ArchivedAt == null && t.PayoutId == null
            )
            .Select(t => new { t.Amount, t.TypeId })
            .ToListAsync();

        var debt = -open.Where(t => HardCode.DEBT_TRANSACTION_TYPES.Contains(t.TypeId))
            .Sum(t => t.Amount);
        var available = open.Where(t =>
                !HardCode.DEBT_TRANSACTION_TYPES.Contains(t.TypeId)
            )
            .Sum(t => t.Amount);

        // Versements figés mais pas encore confirmés par Stripe : l'argent est déjà sorti du
        // solde (lignes tamponnées) sans être encore chez le prof.
        var pendingPayouts = await context
            .TeacherPayouts.Where(p =>
                p.TeacherId == teacherId
                && p.ArchivedAt == null
                && p.StatusId == HardCode.STATUS_TRANSACTION_PENDING
            )
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        return new Response<TeacherWalletBalance>
        {
            Status = 200,
            Message = "Solde récupéré avec succès",
            Data = new TeacherWalletBalance
            {
                Available = available,
                Debt = debt,
                Balance = available - debt,
                PendingPayouts = pendingPayouts,
            },
        };
    }

    /// <summary>
    /// A single wallet transaction with its reservation, student and teacher details.
    /// </summary>
    public async Task<Response<TeacherWalletTransactionDetail>> GetTransactionByIdAsync(
        Guid id,
        CallerIdentity caller
    )
    {
        var transaction = await context.TeacherWalletTransactions
            .Where(t => t.Id == id && t.ArchivedAt == null)
            .Include(t => t.Type)
            .Include(t => t.Teacher)
                .ThenInclude(te => te.User)
            .Include(t => t.Reservation)
                .ThenInclude(r => r.Order)
            .Include(t => t.Reservation)
                .ThenInclude(r => r.Status)
            .Include(t => t.Reservation)
                .ThenInclude(r => r.Cursus)
            .Include(t => t.Reservation)
                .ThenInclude(r => r.Slot)
                    .ThenInclude(s => s.Type)
            .Include(t => t.Reservation)
                .ThenInclude(r => r.Student)
                    .ThenInclude(s => s.User)
            .FirstOrDefaultAsync();

        if (transaction is null || !await CanReadWalletAsync(transaction.TeacherId, caller))
        {
            return new Response<TeacherWalletTransactionDetail>
            {
                Status = 404,
                Message = "Transaction introuvable",
                Data = null,
            };
        }

        return new Response<TeacherWalletTransactionDetail>
        {
            Status = 200,
            Message = "Transaction récupérée avec succès",
            Data = new TeacherWalletTransactionDetail(transaction),
        };
    }
}
