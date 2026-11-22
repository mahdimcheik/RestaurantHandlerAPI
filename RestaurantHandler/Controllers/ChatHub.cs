using System.Text.Json;
using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Controllers;

[Authorize]
public class ChatHub : Hub
{
    private readonly ConnectionManager _connectionManager;
    private readonly MainContext _context;
    private readonly ConversationService _conversationService;

    public ChatHub(
        ConnectionManager connectionManager,
        MainContext context,
        ConversationService conversationService
    )
    {
        _connectionManager = connectionManager;
        _context = context;
        _conversationService = conversationService;
    }

    public override async Task OnConnectedAsync()
    {
        try
        {
            var connectionId = Context.ConnectionId;
            var userName =
                Context.User?.FindFirst("Email")?.Value
                ?? Context.User?.FindFirst("Name")?.Value
                ?? Context.User?.Identity?.Name
                ?? "Anonymous";

            _connectionManager.AddConnection(connectionId, userName);
            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            // Log error if needed
            Console.WriteLine($"Error in OnConnectedAsync: {ex.Message}");
            throw;
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var connectionId = Context.ConnectionId;
            _connectionManager.RemoveConnection(connectionId);
        }
        catch (Exception ex)
        {
            // Log error if needed
            Console.WriteLine($"Error in OnDisconnectedAsync: {ex.Message}");
        }

        await base.OnDisconnectedAsync(exception);
    }

    public Task<int> Ping()
    {
        return Task.FromResult(1);
    }

    /// <summary>
    /// Indicateur de saisie. Seul le type <c>Writing</c> est relayé, uniquement entre les deux
    /// participants de la réservation désignée dans <paramref name="message"/> ; toute autre
    /// demande est ignorée. Les autres événements partent du serveur (<see cref="SignalRNotificationsService"/>).
    /// </summary>
    public async Task SendMessageByUserEmail(string email, string type, JsonElement message)
    {
        try
        {
            if (type != nameof(SignalRNotificationTypeEnum.Writing))
            {
                return;
            }

            var reservationId = ReadGuid(message, "reservationId");
            var sender = GetCaller();
            if (reservationId is null || sender is null)
            {
                return;
            }

            var partnerEmail = await GetPartnerEmailAsync(reservationId.Value, sender.Id);
            if (partnerEmail is null || !SameEmail(partnerEmail, email))
            {
                return;
            }

            var content = ReadString(message, "content") ?? string.Empty;
            await Clients
                .Clients(ConnectionsOf(partnerEmail))
                .SendAsync(
                    type,
                    new ConversationCreate
                    {
                        ReservationId = reservationId.Value,
                        Content = content.Length > 512 ? content[..512] : content,
                        SenderId = sender.Id,
                    }
                );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message to user {email}: {ex.Message}");
        }
    }

    // chat
    /// <summary>
    /// Enregistre le message puis le relaie à l'autre participant de la réservation et à
    /// l'expéditeur. Le destinataire et l'expéditeur sont déduits de la réservation et de la
    /// session, jamais de la requête.
    /// </summary>
    public async Task<ConversationCreate?> SendChatByUserEmail(string email, ConversationCreate message)
    {
        try
        {
            var sender = GetCaller();
            if (sender?.Email is null || message is null)
            {
                return null;
            }

            var saved = await _conversationService.SendMessageAsync(message, sender.Id);
            if (!saved.Data)
            {
                return null;
            }
            message.SenderId = sender.Id;

            var partnerEmail = await GetPartnerEmailAsync(message.ReservationId, sender.Id);
            var recipients = ConnectionsOf(sender.Email);
            if (partnerEmail is not null)
            {
                recipients.AddRange(ConnectionsOf(partnerEmail));
            }

            if (recipients.Count > 0)
            {
                await Clients
                    .Clients(recipients)
                    .SendAsync(nameof(MessageTypeEnum.Chat), message);
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message to user {email}: {ex.Message}");
            return message;
        }
    }

    private UserApp? GetCaller() =>
        Context.User is null ? null : CheckUser.GetUserFromClaim(Context.User, _context);

    /// <summary>L'autre participant de la réservation, ou <c>null</c> si l'appelant n'y participe pas.</summary>
    private async Task<string?> GetPartnerEmailAsync(Guid reservationId, Guid callerId)
    {
        var participants = await _context
            .Reservations.Where(r => r.Id == reservationId)
            .Select(r => new
            {
                StudentUserId = r.Student!.UserId,
                StudentEmail = r.Student!.User!.Email,
                TeacherUserId = r.Slot!.Teacher!.UserId,
                TeacherEmail = r.Slot!.Teacher!.User!.Email,
            })
            .FirstOrDefaultAsync();

        if (participants is null)
        {
            return null;
        }
        if (participants.StudentUserId == callerId)
        {
            return participants.TeacherEmail;
        }
        if (participants.TeacherUserId == callerId)
        {
            return participants.StudentEmail;
        }
        return null;
    }

    private List<string> ConnectionsOf(string email) =>
        _connectionManager
            .GetAllConnections()
            .Where(kvp => SameEmail(kvp.Value, email))
            .Select(kvp => kvp.Key)
            .ToList();

    private static bool SameEmail(string? a, string? b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static JsonElement? Property(JsonElement message, string name)
    {
        if (message.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        foreach (var property in message.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }
        return null;
    }

    private static Guid? ReadGuid(JsonElement message, string name) =>
        Property(message, name) is { ValueKind: JsonValueKind.String } value
        && Guid.TryParse(value.GetString(), out var id)
            ? id
            : null;

    private static string? ReadString(JsonElement message, string name) =>
        Property(message, name) is { ValueKind: JsonValueKind.String } value
            ? value.GetString()
            : null;
}
