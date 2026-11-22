using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

public class NotificationService
{
    private readonly MainContext _context;

    public NotificationService(MainContext context)
    {
        _context = context;
    }

    public async Task<Response<List<NotificationDetails>>> GetNotificationsByUserAsync(
        FilterNotification filter,
        CallerIdentity? userPrincipal
    )
    {
        var user = CheckUser.GetUser(userPrincipal, _context);
        if (user == null)
            return new Response<List<NotificationDetails>>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
            };

        var query = _context.Notifications
            .Where(n => n.UserId == user.Id);

        var total = await query.Where(n => !n.IsSeen).CountAsync(); // 

        if (filter.IsSeen.HasValue)
            query = query.Where(n => n.IsSeen == filter.IsSeen.Value);


        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenBy(n => n.Id)
            .Skip(filter.First)
            .Take(filter.Row ?? 10)
            .Select(n => new NotificationDetails
            {
                Id = n.Id,
                Name = n.Name,
                Message = n.Message,
                IsSeen = n.IsSeen,
                CreatedAt = n.CreatedAt,
                Link = n.Link,
            })
            .ToListAsync();

        return new Response<List<NotificationDetails>>
        {
            Status = 200,
            Message = "Notifications récupérées avec succès",
            Data = notifications,
            Count = total,
        };
    }

    public async Task<Response<NotificationDetails>> ToggleSeenAsync(
        Guid notificationId,
        CallerIdentity? userPrincipal
    )
    {
        var user = CheckUser.GetUser(userPrincipal, _context);
        if (user == null)
            return new Response<NotificationDetails>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
            };

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == user.Id);

        if (notification == null)
            return new Response<NotificationDetails>
            {
                Status = 404,
                Message = "Notification non trouvée",
            };

        notification.IsSeen = !notification.IsSeen;
        notification.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();

        return new Response<NotificationDetails>
        {
            Status = 200,
            Message = notification.IsSeen
                ? "Notification marquée comme vue"
                : "Notification marquée comme non vue",
            Data = new NotificationDetails
            {
                Id = notification.Id,
                Name = notification.Name,
                Message = notification.Message,
                IsSeen = notification.IsSeen,
                CreatedAt = notification.CreatedAt,
                Link = notification.Link,
            },
        };
    }

    public async Task<Response<object>> ToggleAllSeenAsync(
       CallerIdentity? userPrincipal
   )
    {
        var user = CheckUser.GetUser(userPrincipal, _context);
        if (user == null)
            return new Response<object>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
            };

        var notification = await _context.Notifications
            .Where(n =>  n.UserId == user.Id && !n.IsSeen).ExecuteUpdateAsync((p) => p.SetProperty(no => no.IsSeen, true) );           

        await _context.SaveChangesAsync();

        return new Response<object>
        {
            Status = 200,
            Message = "Ok"
           
        };
    }
}
