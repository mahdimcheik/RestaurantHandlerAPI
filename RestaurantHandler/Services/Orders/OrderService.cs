using System.Text.Json;
using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

public class OrderService(MainContext context)
{
    public async Task<Response<List<OrderDetails>>> GetOrdersByStudentAsync(
        CustomTableState query,
        CallerIdentity? principals
    )
    {
        var user = CheckUser.GetUser(principals, context);
        if (user is null)
        {
            return new Response<List<OrderDetails>>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
                Data = null,
            };
        }

        IQueryable<Order> orders = context
            .Orders.Where(o =>
                o.StudentId == user.Id
                && o.ArchivedAt == null
                // Une commande sans paiement n'est qu'un regroupement interne de
                // réservations (aucun argent, aucune facture) : élèves et profs ne la
                // voient pas, ses réservations vivent dans les pages Réservations.
                && o.PaymentRequired
                && o.StatusId != HardCode.STATUS_ORDER_PENDING
            )
            .Include(o => o.Status)
            .Include(o => o.Student)
            .ThenInclude(s => s.User)
            .Include(o => o.Teacher)
            .ThenInclude(t => t.User)
            .Include(o => o.Teacher)
            .ThenInclude(t => t.Cursuses)
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .ThenInclude(r => r.Status)
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .ThenInclude(r => r.Slot)
            .ThenInclude(s => s.Type)
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .ThenInclude(r => r.Slot)
            .ThenInclude(s => s.Teacher)
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .ThenInclude(r => r.Cursus)
            .Include(o => o.Refunds)
            .ThenInclude(r => r.Status);

        if (
            query.Filters?.TryGetValue("status", out Filter? statuses) == true
            && statuses != null
        )
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
            var trimmed = orderNumber.Value.ToString().Trim();
            orders = orders.Where(o => EF.Functions.ILike(o.OrderNumber, $"%{trimmed}%"));
        }

        if (!string.IsNullOrEmpty(query.Search?.Trim()))
        {
            var trimmed = query.Search.Trim();
            orders = orders.Where(o => EF.Functions.ILike(o.OrderNumber, $"%{trimmed}%"));
        }

        var count = await orders.CountAsync();

        if (query.Rows <= 0)
            query.Rows = 10;

        orders = orders.OrderByDescending(o => o.OrderDate).ThenBy(o => o.Id).Skip(query.First).Take(query.Rows);

        return new Response<List<OrderDetails>>
        {
            Status = 200,
            Message = "Commandes récupérées avec succès",
            Data = orders.Select(o => new OrderDetails(o)).ToList(),
            Count = count,
        };
    }

    // Teacher-facing list of their own orders. Kept separate from the student method
    // on purpose: this one will grow its own (different) filters later.
    public async Task<Response<List<OrderDetails>>> GetOrdersByTeacherAsync(
        CustomTableState query,
        CallerIdentity? principals
    )
    {
        var user = CheckUser.GetUser(principals, context);
        if (user is null)
        {
            return new Response<List<OrderDetails>>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
                Data = null,
            };
        }

        var teacher = await context.Teachers.FirstOrDefaultAsync(t => t.UserId == user.Id);
        if (teacher is null)
        {
            return new Response<List<OrderDetails>>
            {
                Status = 404,
                Message = "Professeur introuvable",
                Data = null,
            };
        }

        IQueryable<Order> orders = context
            .Orders.Where(o =>
                o.TeacherId == teacher.Id
                && o.ArchivedAt == null
                // Cf. GetOrdersByStudentAsync : commandes sans paiement masquées.
                && o.PaymentRequired
                && o.StatusId != HardCode.STATUS_ORDER_PENDING
            )
            .Include(o => o.Status)
            .Include(o => o.Student)
            .ThenInclude(s => s.User)
            .Include(o => o.Teacher)
            .ThenInclude(t => t.User)
            .Include(o => o.Teacher)
            .ThenInclude(t => t.Cursuses)
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .ThenInclude(r => r.Status)
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .ThenInclude(r => r.Slot)
            .ThenInclude(s => s.Type)
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .ThenInclude(r => r.Slot)
            .ThenInclude(s => s.Teacher)
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .ThenInclude(r => r.Cursus)
            .Include(o => o.Refunds)
            .ThenInclude(r => r.Status);

        if (
            query.Filters?.TryGetValue("status", out Filter? statuses) == true
            && statuses != null
        )
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
            var trimmed = orderNumber.Value.ToString().Trim();
            orders = orders.Where(o => EF.Functions.ILike(o.OrderNumber, $"%{trimmed}%"));
        }

        if (!string.IsNullOrEmpty(query.Search?.Trim()))
        {
            var trimmed = query.Search.Trim();
            orders = orders.Where(o => EF.Functions.ILike(o.OrderNumber, $"%{trimmed}%"));
        }

        var count = await orders.CountAsync();

        if (query.Rows <= 0)
            query.Rows = 10;

        orders = orders.OrderByDescending(o => o.OrderDate).ThenBy(o => o.Id).Skip(query.First).Take(query.Rows);

        return new Response<List<OrderDetails>>
        {
            Status = 200,
            Message = "Commandes récupérées avec succès",
            Data = orders.Select(o => new OrderDetails(o)).ToList(),
            Count = count,
        };
    }

    // Détail d'une commande. Un seul endpoint pour les trois rôles : l'accès est
    // filtré sur l'appelant (élève propriétaire / prof de la commande / admin), donc
    // une commande qu'on n'a pas le droit de voir est un 404, pas un 403.
    public async Task<Response<OrderDetails>> GetOrderByIdAsync(
        Guid orderId,
        CallerIdentity? principals
    )
    {
        var user = CheckUser.GetUser(principals, context);
        if (user is null)
        {
            return new Response<OrderDetails>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
                Data = null,
            };
        }

        var isAdmin = principals?.IsAdmin == true;

        // Le prof porte son propre Id (≠ UserId) sur Order.TeacherId.
        Guid? teacherId = null;
        if (!isAdmin)
        {
            teacherId = await context
                .Teachers.Where(t => t.UserId == user.Id)
                .Select(t => (Guid?)t.Id)
                .FirstOrDefaultAsync();
        }

        var order = await context
            .Orders.Where(o => o.Id == orderId && o.ArchivedAt == null)
            .Where(o =>
                isAdmin
                || o.StudentId == user.Id
                || (teacherId != null && o.TeacherId == teacherId)
            )
            .Include(o => o.Status)
            .Include(o => o.Student)
            .ThenInclude(s => s.User)
            .Include(o => o.Teacher)
            .ThenInclude(t => t.User)
            .Include(o => o.Teacher)
            .ThenInclude(t => t.Cursuses)
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .ThenInclude(r => r.Status)
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .ThenInclude(r => r.Slot)
            .ThenInclude(s => s.Type)
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .ThenInclude(r => r.Slot)
            .ThenInclude(s => s.Teacher)
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .ThenInclude(r => r.Cursus)
            .Include(o => o.Refunds)
            .ThenInclude(r => r.Status)
            .FirstOrDefaultAsync();

        if (order is null)
        {
            return new Response<OrderDetails>
            {
                Status = 404,
                Message = "Commande introuvable",
                Data = null,
            };
        }

        return new Response<OrderDetails>
        {
            Status = 200,
            Message = "Commande récupérée avec succès",
            Data = new OrderDetails(order),
        };
    }

    public async Task<Response<List<StatusOrderDetails>>> GetAllOrderStatusesAsync()
    {
        var statuses = await context
            .StatusOrders.AsNoTracking()
            .Where(s => s.ArchivedAt == null)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new StatusOrderDetails(s))
            .ToListAsync();

        return new Response<List<StatusOrderDetails>>
        {
            Status = 200,
            Message = "Statuts de commande récupérés avec succès",
            Data = statuses,
            Count = statuses.Count,
        };
    }

    // Orders are teacher-scoped, so a student can have several concurrent PENDING
    // baskets (one per teacher). Returns all of them; an empty cart returns []. Orders
    // are now created on booking (ReservationCartService.GetActiveOrder), never on cart-view,
    // so this is a pure read with no lazy creation.
    public async Task<Response<List<OrderDetails>>> GetActiveOrderForStudentAsync(
        CallerIdentity? principals
    )
    {
        var user = CheckUser.GetUser(principals, context);
        if (user is null)
        {
            return new Response<List<OrderDetails>>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
                Data = null,
            };
        }

        var orders = await context
            .Orders.Where(o =>
                o.StudentId == user.Id
                && o.ArchivedAt == null
                && o.StatusId == HardCode.STATUS_ORDER_PENDING
                // Cf. GetOrdersByStudentAsync : commandes sans paiement masquées.
                && o.PaymentRequired
                // Une commande libérée faute de paiement (ReleasePendingOrder → Order.Reset)
                // reste PENDING, non archivée et sans deadline, mais vidée de ses
                // réservations — un panier vide, pas une commande à payer.
                && o.Reservations.Any(r =>
                    r.ArchivedAt == null && r.StatusId == HardCode.RESERVATION_PENDING
                )
                // Délai de paiement dépassé : plus payable (garde du checkout), donc plus
                // active, même si CleanStalePendingOrders ne l'a pas encore libérée.
                && (o.PaymentDeadline == null || o.PaymentDeadline > DateTimeOffset.UtcNow)
            )
            .Include(o => o.Status)
            .Include(o => o.Student)
            .ThenInclude(s => s.User)
            .Include(o => o.Teacher)
            .ThenInclude(t => t.User)
            .Include(o => o.Teacher)
            .ThenInclude(t => t.Cursuses)
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .ThenInclude(r => r.Status)
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .ThenInclude(r => r.Cursus)
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .ThenInclude(r => r.Slot)
            .ThenInclude(s => s.Type)
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .ThenInclude(r => r.Cursus)
            .Include(o => o.Refunds)
            .ThenInclude(r => r.Status)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return new Response<List<OrderDetails>>
        {
            Status = 200,
            Message = "Commandes actives récupérées avec succès",
            Data = orders.Select(o => new OrderDetails(o)).ToList(),
        };
    }

    // admins
    public async Task<Response<List<OrderDetails>>> GetOrdersByAdmins(CustomTableState query)
    {
        IQueryable<Order> orders = context
            .Orders.Include(o => o.Status)
            .Include(o => o.Student)
            .ThenInclude(s => s.User)
            .Include(o => o.Teacher)
            .ThenInclude(t => t.User)
            .Include(o => o.Teacher)
            .ThenInclude(t => t.Cursuses)
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .ThenInclude(r => r.Status)
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .ThenInclude(r => r.Slot)
            .ThenInclude(s => s.Type)
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .ThenInclude(r => r.Slot)
            .ThenInclude(s => s.Teacher)
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .ThenInclude(r => r.Cursus)
            .Include(o => o.Refunds)
            .ThenInclude(r => r.Status)
            .OrderBy(o => o.Payment.CreatedAt);

           
        if (query.Filters?.TryGetValue("fullName", out Filter? fullName) == true )
        {
            var toto = fullName;
            var trimmed = fullName.Value.ToString().Trim().ToLower();
            orders = orders.Where(o =>
                EF.Functions.ILike(
                    o.Student.User.FirstName.ToLower()
                        + " "
                        + o.Student.User.LastName.ToLower(),
                    $"%{trimmed}%"
                )
            );
        }

        if (query.Filters?.TryGetValue("studentId", out Filter? studentId) == true)
        {
            var trimmed = studentId.Value.ToString().Trim().ToLower();
            orders = orders.Where(o =>
                EF.Functions.ILike(o.StudentId.ToString().ToLower(), $"%{trimmed}%")
            );
        }            

        if (
            query.Filters?.TryGetValue("status", out Filter? statuses) == true
            && statuses != null
        )
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
            var trimmed = orderNumber.Value.ToString().Trim();
            orders = orders.Where(o => EF.Functions.ILike(o.OrderNumber, $"%{trimmed}%"));
        }

        if (!string.IsNullOrEmpty(query.Search?.Trim()))
        {
            var trimmed = query.Search.Trim();
            orders = orders.Where(o => EF.Functions.ILike(o.OrderNumber, $"%{trimmed}%"));
        }

        var count = await orders.CountAsync();

        if (query.Rows <= 0)
            query.Rows = 10;

        orders = orders.OrderByDescending(o => o.OrderDate).ThenBy(o => o.Id).Skip(query.First).Take(query.Rows);

        return new Response<List<OrderDetails>>
        {
            Status = 200,
            Message = "Commandes récupérées avec succès",
            Data = orders.Select(o => new OrderDetails(o)).ToList(),
            Count = count,
        };
    }
}
