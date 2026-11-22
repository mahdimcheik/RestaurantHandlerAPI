using System.Text.Json;
using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

public class MonthlyReportService(MainContext context, OrderAccountingService accounting)
{
    // ---------------------------------------------------------------------
    // Bilan mensuel (admin + prof) & export CSV
    // ---------------------------------------------------------------------

    // Périmètre d'un bilan : les commandes ENCAISSÉES dans le mois. Le mois est celui
    // du paiement (Payment.CreatedAt), pas de la commande — une commande créée le 30/06
    // et payée le 01/07 appartient au bilan de juillet. Une commande impayée n'a pas de
    // Payment : elle sort d'elle-même, inutile d'exclure le statut PENDING. Même chose,
    // par construction, pour une commande sans paiement (Order.PaymentRequired == false,
    // flux prof non-onboardé) : elle n'a et n'aura jamais de Payment, donc jamais de
    // bilan financier — ne PAS remplacer ce filtre par un join sur PaymentRequired.
    private IQueryable<Order> MonthlyScope(
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? teacherId,
        CustomTableState query
    )
    {
        IQueryable<Order> orders = context
            .Orders.Where(o =>
                o.ArchivedAt == null
                && o.Payment != null
                && o.Payment.CreatedAt >= from
                && o.Payment.CreatedAt < to
            )
            .Include(o => o.Status)
            .Include(o => o.Payment)
            .Include(o => o.Student)
            .ThenInclude(s => s.User)
            .Include(o => o.Teacher)
            .ThenInclude(t => t.User)
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .Include(o => o.Refunds);

        if (teacherId != null)
            orders = orders.Where(o => o.TeacherId == teacherId);

        if (query.Filters?.TryGetValue("status", out Filter? statuses) == true && statuses != null)
        {
            var ids = JsonSerializer.Deserialize<Guid[]>(statuses.Value?.ToString() ?? "[]");
            if (ids != null && ids.Length > 0)
                orders = orders.Where(o => ids.Contains(o.StatusId));
        }

        if (
            query.Filters?.TryGetValue("orderNumber", out Filter? orderNumber) == true
            && orderNumber != null
        )
        {
            var trimmed = orderNumber.Value.ToString()?.Trim() ?? "";
            orders = orders.Where(o => EF.Functions.ILike(o.OrderNumber, $"%{trimmed}%"));
        }

        if (query.Filters?.TryGetValue("fullName", out Filter? fullName) == true && fullName != null)
        {
            var trimmed = fullName.Value.ToString()?.Trim().ToLower() ?? "";
            orders = orders.Where(o =>
                EF.Functions.ILike(
                    o.Student.User.FirstName.ToLower() + " " + o.Student.User.LastName.ToLower(),
                    $"%{trimmed}%"
                )
            );
        }

        if (!string.IsNullOrEmpty(query.Search?.Trim()))
        {
            var trimmed = query.Search.Trim();
            orders = orders.Where(o => EF.Functions.ILike(o.OrderNumber, $"%{trimmed}%"));
        }

        return orders.OrderByDescending(o => o.Payment!.CreatedAt).ThenBy(o => o.Id);
    }

    private static string FullName(UserApp? user) =>
        user is null ? string.Empty : $"{user.FirstName} {user.LastName}".Trim();

    private async Task<List<MonthlyReportRow>> BuildAdminRowsAsync(List<Order> orders)
    {
        var walletLines = await accounting.LoadWalletLinesAsync(orders.Select(o => o.Id).ToList());

        return orders
            .Select(o =>
            {
                var accounting = OrderAccountingService.BuildAccounting(o, walletLines[o.Id]);
                return new MonthlyReportRow
                {
                    OrderId = o.Id,
                    OrderNumber = o.OrderNumber,
                    OrderDate = o.OrderDate,
                    PaidAt = o.Payment?.CreatedAt,
                    StatusName = o.Status?.Name,
                    StudentName = FullName(o.Student?.User),
                    TeacherName = FullName(o.Teacher?.User),
                    ReservationsCount = accounting.Lines.Count,
                    GrossPaid = accounting.GrossPaid,
                    StripeFee = accounting.StripeFee,
                    StripeFeePending = accounting.StripeFeePending,
                    NetCollected = accounting.NetCollected,
                    RefundedAmount = accounting.RefundedAmount,
                    TeacherOwed = accounting.TeacherOwed,
                    TeacherPaidOut = accounting.TeacherPaidOut,
                    TeacherTotal = accounting.TeacherTotal,
                    PlatformGain = accounting.PlatformGain,
                    PlatformGainProvisional = accounting.PlatformGainProvisional,
                    Lines = accounting.Lines,
                };
            })
            .ToList();
    }

    private async Task<List<TeacherMonthlyReportRow>> BuildTeacherRowsAsync(List<Order> orders)
    {
        var walletLines = await accounting.LoadWalletLinesAsync(orders.Select(o => o.Id).ToList());

        return orders
            .Select(o =>
            {
                var accounting = OrderAccountingService.BuildAccounting(o, walletLines[o.Id]);
                return new TeacherMonthlyReportRow
                {
                    OrderId = o.Id,
                    OrderNumber = o.OrderNumber,
                    PaidAt = o.Payment?.CreatedAt,
                    StatusName = o.Status?.Name,
                    StudentName = FullName(o.Student?.User),
                    ReservationsCount = accounting.Lines.Count,
                    GrossPaid = accounting.GrossPaid,
                    // Vue prof : ce que la plateforme retient sur ce qui lui reste acquis.
                    // Jamais NetCollected ni les frais Stripe (données plateforme).
                    Commission =
                        accounting.GrossPaid
                        - accounting.RefundedAmount
                        - accounting.TeacherTotal,
                    RefundedAmount = accounting.RefundedAmount,
                    TeacherOwed = accounting.TeacherOwed,
                    TeacherPaidOut = accounting.TeacherPaidOut,
                    TeacherTotal = accounting.TeacherTotal,
                    Lines = accounting.Lines,
                };
            })
            .ToList();
    }

    // Totaux du mois entier (filtres appliqués), pas seulement la page affichée.
    // Mêmes formules que BuildAccounting => Totals == somme des lignes, par construction.
    private async Task<(
        decimal GrossPaid,
        decimal StripeFee,
        bool StripeFeePending,
        decimal NetCollected,
        decimal Refunded,
        decimal TeacherOwed,
        decimal TeacherPaidOut,
        int OrdersCount
    )> AggregateMonthAsync(IQueryable<Order> scope)
    {
        var orderIds = await scope.Select(o => o.Id).ToListAsync();

        var payments = await context
            .Payments.Where(p => orderIds.Contains(p.OrderId))
            .Select(p => new
            {
                p.Amount,
                p.StripeFee,
                p.NetAmount,
            })
            .ToListAsync();

        var refunded =
            await context
                .Refunds.Where(r => orderIds.Contains(r.OrderId))
                .SumAsync(r => (decimal?)r.Amount) ?? 0m;

        var wallet = await context
            .TeacherWalletTransactions.Where(w =>
                w.ArchivedAt == null // cf. LoadWalletLinesAsync : une ligne scindée est archivée
                && w.ReservationId != null
                && w.Reservation!.OrderId != null
                && orderIds.Contains(w.Reservation.OrderId.Value)
            )
            .Select(w => new { w.Amount, w.PayoutId })
            .ToListAsync();

        return (
            GrossPaid: payments.Sum(p => p.Amount),
            StripeFee: payments.Sum(p => p.StripeFee ?? 0m),
            StripeFeePending: payments.Any(p => p.StripeFee == null),
            NetCollected: payments.Sum(p => p.NetAmount ?? (p.Amount - (p.StripeFee ?? 0m))),
            Refunded: refunded,
            TeacherOwed: wallet.Where(w => w.PayoutId == null).Sum(w => w.Amount),
            TeacherPaidOut: wallet.Where(w => w.PayoutId != null).Sum(w => w.Amount),
            OrdersCount: orderIds.Count
        );
    }

    private async Task<MonthlyReportTotals> ComputeAdminTotalsAsync(IQueryable<Order> scope)
    {
        var a = await AggregateMonthAsync(scope);
        var teacherTotal = a.TeacherOwed + a.TeacherPaidOut;

        return new MonthlyReportTotals
        {
            OrdersCount = a.OrdersCount,
            GrossPaid = a.GrossPaid,
            StripeFee = a.StripeFee,
            StripeFeePending = a.StripeFeePending,
            NetCollected = a.NetCollected,
            RefundedAmount = a.Refunded,
            TeacherOwed = a.TeacherOwed,
            TeacherPaidOut = a.TeacherPaidOut,
            TeacherTotal = teacherTotal,
            PlatformGain = a.NetCollected - a.Refunded - teacherTotal,
            PlatformGainProvisional = a.StripeFeePending,
        };
    }

    private async Task<TeacherMonthlyReportTotals> ComputeTeacherTotalsAsync(IQueryable<Order> scope)
    {
        var a = await AggregateMonthAsync(scope);
        var teacherTotal = a.TeacherOwed + a.TeacherPaidOut;

        return new TeacherMonthlyReportTotals
        {
            OrdersCount = a.OrdersCount,
            GrossPaid = a.GrossPaid,
            Commission = a.GrossPaid - a.Refunded - teacherTotal,
            RefundedAmount = a.Refunded,
            TeacherOwed = a.TeacherOwed,
            TeacherPaidOut = a.TeacherPaidOut,
            TeacherTotal = teacherTotal,
        };
    }

    // Traduit l'utilisateur connecté en prof : user.Id N'EST PAS Order.TeacherId.
    private async Task<Guid?> GetTeacherIdAsync(Guid userId) =>
        await context
            .Teachers.Where(t => t.UserId == userId)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync();

    public async Task<
        Response<MonthlyReport<MonthlyReportRow, MonthlyReportTotals>>
    > GetAdminMonthlyReportAsync(MonthlyReportQuery query)
    {
        if (!MonthRange.IsValid(query.Year, query.Month))
        {
            return new Response<MonthlyReport<MonthlyReportRow, MonthlyReportTotals>>
            {
                Status = 400,
                Message = "Mois invalide",
                Data = null,
            };
        }

        var (from, to) = MonthRange.ForMonth(query.Year, query.Month);
        var scope = MonthlyScope(from, to, null, query);

        var count = await scope.CountAsync();

        if (query.Rows <= 0)
            query.Rows = 10;

        var page = await scope.Skip(query.First).Take(query.Rows).ToListAsync();

        var report = new MonthlyReport<MonthlyReportRow, MonthlyReportTotals>
        {
            Year = query.Year,
            Month = query.Month,
            From = from,
            To = to,
            Totals = await ComputeAdminTotalsAsync(scope),
            Orders = await BuildAdminRowsAsync(page),
        };

        return new Response<MonthlyReport<MonthlyReportRow, MonthlyReportTotals>>
        {
            Status = 200,
            Message = "Bilan mensuel récupéré avec succès",
            Data = report,
            Count = count,
        };
    }

    public async Task<
        Response<MonthlyReport<TeacherMonthlyReportRow, TeacherMonthlyReportTotals>>
    > GetTeacherMonthlyReportAsync(MonthlyReportQuery query, CallerIdentity? principals)
    {
        if (!MonthRange.IsValid(query.Year, query.Month))
        {
            return new Response<
                MonthlyReport<TeacherMonthlyReportRow, TeacherMonthlyReportTotals>
            >
            {
                Status = 400,
                Message = "Mois invalide",
                Data = null,
            };
        }

        var user = CheckUser.GetUser(principals, context);
        if (user is null)
        {
            return new Response<
                MonthlyReport<TeacherMonthlyReportRow, TeacherMonthlyReportTotals>
            >
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
                Data = null,
            };
        }

        var teacherId = await GetTeacherIdAsync(user.Id);
        if (teacherId is null)
        {
            return new Response<
                MonthlyReport<TeacherMonthlyReportRow, TeacherMonthlyReportTotals>
            >
            {
                Status = 404,
                Message = "Profil professeur introuvable",
                Data = null,
            };
        }

        var (from, to) = MonthRange.ForMonth(query.Year, query.Month);
        var scope = MonthlyScope(from, to, teacherId, query);

        var count = await scope.CountAsync();

        if (query.Rows <= 0)
            query.Rows = 10;

        var page = await scope.Skip(query.First).Take(query.Rows).ToListAsync();

        var report = new MonthlyReport<TeacherMonthlyReportRow, TeacherMonthlyReportTotals>
        {
            Year = query.Year,
            Month = query.Month,
            From = from,
            To = to,
            Totals = await ComputeTeacherTotalsAsync(scope),
            Orders = await BuildTeacherRowsAsync(page),
        };

        return new Response<
            MonthlyReport<TeacherMonthlyReportRow, TeacherMonthlyReportTotals>
        >
        {
            Status = 200,
            Message = "Bilan mensuel récupéré avec succès",
            Data = report,
            Count = count,
        };
    }

    // Export CSV : même périmètre et mêmes filtres que la liste, mais SANS pagination —
    // on exporte le mois entier tel qu'il est filtré à l'écran.
    public async Task<Response<CsvFile>> ExportAdminMonthlyReportCsvAsync(MonthlyReportQuery query)
    {
        if (!MonthRange.IsValid(query.Year, query.Month))
        {
            return new Response<CsvFile>
            {
                Status = 400,
                Message = "Mois invalide",
                Data = null,
            };
        }

        var (from, to) = MonthRange.ForMonth(query.Year, query.Month);
        var scope = MonthlyScope(from, to, null, query);

        var orders = await scope.ToListAsync();
        var rows = await BuildAdminRowsAsync(orders);
        var totals = await ComputeAdminTotalsAsync(scope);

        string[] headers =
        [
            "N° commande",
            "Date paiement",
            "Date commande",
            "Statut",
            "Élève",
            "Prof",
            "Nb réservations",
            "Brut payé",
            "Frais Stripe",
            "Net encaissé",
            "Remboursé",
            "Dû au prof",
            "Versé au prof",
            "Total prof",
            "Gain plateforme",
            "Provisoire",
        ];

        var lines = rows.Select(r => new List<string?>
            {
                r.OrderNumber,
                Csv.Date(r.PaidAt),
                Csv.Date(r.OrderDate),
                r.StatusName,
                r.StudentName,
                r.TeacherName,
                r.ReservationsCount.ToString(),
                Csv.Money(r.GrossPaid),
                Csv.Money(r.StripeFee),
                Csv.Money(r.NetCollected),
                Csv.Money(r.RefundedAmount),
                Csv.Money(r.TeacherOwed),
                Csv.Money(r.TeacherPaidOut),
                Csv.Money(r.TeacherTotal),
                Csv.Money(r.PlatformGain),
                Csv.Bool(r.PlatformGainProvisional),
            })
            .ToList();

        lines.Add(
            [
                "TOTAL",
                "",
                "",
                "",
                "",
                "",
                totals.OrdersCount.ToString(),
                Csv.Money(totals.GrossPaid),
                Csv.Money(totals.StripeFee),
                Csv.Money(totals.NetCollected),
                Csv.Money(totals.RefundedAmount),
                Csv.Money(totals.TeacherOwed),
                Csv.Money(totals.TeacherPaidOut),
                Csv.Money(totals.TeacherTotal),
                Csv.Money(totals.PlatformGain),
                Csv.Bool(totals.PlatformGainProvisional),
            ]
        );

        return new Response<CsvFile>
        {
            Status = 200,
            Message = "Export généré avec succès",
            Data = new CsvFile
            {
                FileName = $"bilan-{query.Year}-{query.Month:00}.csv",
                Content = Csv.Build(headers, lines),
            },
            Count = rows.Count,
        };
    }

    public async Task<Response<CsvFile>> ExportTeacherMonthlyReportCsvAsync(
        MonthlyReportQuery query,
        CallerIdentity? principals
    )
    {
        if (!MonthRange.IsValid(query.Year, query.Month))
        {
            return new Response<CsvFile>
            {
                Status = 400,
                Message = "Mois invalide",
                Data = null,
            };
        }

        var user = CheckUser.GetUser(principals, context);
        if (user is null)
        {
            return new Response<CsvFile>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
                Data = null,
            };
        }

        var teacherId = await GetTeacherIdAsync(user.Id);
        if (teacherId is null)
        {
            return new Response<CsvFile>
            {
                Status = 404,
                Message = "Profil professeur introuvable",
                Data = null,
            };
        }

        var (from, to) = MonthRange.ForMonth(query.Year, query.Month);
        var scope = MonthlyScope(from, to, teacherId, query);

        var orders = await scope.ToListAsync();
        var rows = await BuildTeacherRowsAsync(orders);
        var totals = await ComputeTeacherTotalsAsync(scope);

        string[] headers =
        [
            "N° commande",
            "Date paiement",
            "Statut",
            "Élève",
            "Nb réservations",
            "Brut payé",
            "Commission",
            "Remboursé",
            "Dû",
            "Versé",
            "Total",
        ];

        var lines = rows.Select(r => new List<string?>
            {
                r.OrderNumber,
                Csv.Date(r.PaidAt),
                r.StatusName,
                r.StudentName,
                r.ReservationsCount.ToString(),
                Csv.Money(r.GrossPaid),
                Csv.Money(r.Commission),
                Csv.Money(r.RefundedAmount),
                Csv.Money(r.TeacherOwed),
                Csv.Money(r.TeacherPaidOut),
                Csv.Money(r.TeacherTotal),
            })
            .ToList();

        lines.Add(
            [
                "TOTAL",
                "",
                "",
                "",
                totals.OrdersCount.ToString(),
                Csv.Money(totals.GrossPaid),
                Csv.Money(totals.Commission),
                Csv.Money(totals.RefundedAmount),
                Csv.Money(totals.TeacherOwed),
                Csv.Money(totals.TeacherPaidOut),
                Csv.Money(totals.TeacherTotal),
            ]
        );

        return new Response<CsvFile>
        {
            Status = 200,
            Message = "Export généré avec succès",
            Data = new CsvFile
            {
                FileName = $"bilan-prof-{query.Year}-{query.Month:00}.csv",
                Content = Csv.Build(headers, lines),
            },
            Count = rows.Count,
        };
    }
}
