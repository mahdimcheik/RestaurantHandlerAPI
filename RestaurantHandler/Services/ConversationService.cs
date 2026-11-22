using RestaurantHandler.Contexts;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Services;

public class ConversationService(
    MainContext context,
    StorageService storage,
    SignalRNotificationsService signalR,
    PushNotificationService push
)
{
    /// <summary>
    /// Enregistre un message de <paramref name="senderUserId"/>, qui doit participer à la
    /// réservation. <see cref="ConversationCreate.SenderId"/> est ignoré : l'expéditeur vient
    /// toujours de la session.
    /// </summary>
    public async Task<Response<bool>> SendMessageAsync(
        ConversationCreate dto,
        Guid senderUserId
    )
    {
        if (!await ReservationAccess.IsParticipantAsync(context, dto.ReservationId, senderUserId))
            return new Response<bool>
            {
                Status = 404,
                Message = "Réservation introuvable",
                Data = false,
            };

        var conversation = new Conversation
        {
            Content = dto.Content,
            ReservationId = dto.ReservationId,
            SenderId = senderUserId,
        };

        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();

        push.EnqueueChatMessage(conversation.Id);

        return new Response<bool>
        {
            Status = 200,
            Message = "Message envoyé avec succès",
            Data = true,
        };
    }

    public async Task<Response<List<ConversationDetails>>> GetConversationByReservationIdAsync(
        Guid reservationId,
        CallerIdentity? principal
    )
    {
        var user = CheckUser.GetUser(principal, context);
        if (user is null)
            return new Response<List<ConversationDetails>>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
            };

        var canRead = principal?.IsAdmin == true
            ? await context.Reservations.AnyAsync(r => r.Id == reservationId)
            : await ReservationAccess.IsParticipantAsync(context, reservationId, user.Id);

        if (!canRead)
            return new Response<List<ConversationDetails>>
            {
                Status = 404,
                Message = "Réservation introuvable",
            };

        var rows = await context
            .Conversations.Where(c => c.ReservationId == reservationId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.Content,
                c.ReservationId,
                c.CreatedAt,
                c.SenderId,
                Attachment = c.Attachment == null
                    ? null
                    : new
                    {
                        c.Attachment.Id,
                        c.Attachment.FileName,
                        c.Attachment.MimeType,
                        c.Attachment.Size,
                        c.Attachment.FilePath,
                        c.Attachment.UploadedAt,
                    },
            })
            .ToListAsync();
        var conversations = rows.Select(c => new ConversationDetails
            {
                Id = c.Id,
                Content = c.Content,
                ReservationId = c.ReservationId,
                CreatedAt = c.CreatedAt,
                SenderId = c.SenderId,
                Attachment = c.Attachment is null
                    ? null
                    : new ConversationAttachmentDetails
                    {
                        Id = c.Attachment.Id,
                        FileName = c.Attachment.FileName,
                        MimeType = c.Attachment.MimeType,
                        Size = c.Attachment.Size,
                        Url = storage.Url(c.Attachment.FilePath),
                        UploadedAt = c.Attachment.UploadedAt,
                    },
            })
            .ToList();

        return new Response<List<ConversationDetails>>
        {
            Status = 200,
            Message = "Conversations récupérées avec succès",
            Data = conversations,
            Count = conversations.Count,
        };
    }

    /// <summary>
    /// Envoie un message portant un fichier (ADR-0016), avec une légende facultative. Le plafond
    /// par réservation est vérifié sous le verrou de la réservation ; un objet déposé dont le
    /// message n'a pu être enregistré est supprimé. Les deux participants reçoivent l'événement
    /// <c>Chat</c>, comme pour un message texte.
    /// </summary>
    public async Task<Response<ConversationDetails>> SendAttachmentAsync(
        Guid reservationId,
        ConversationAttachmentUpload dto,
        CallerIdentity? caller,
        CancellationToken ct = default
    )
    {
        var sender = CheckUser.GetUser(caller, context);
        if (sender is null)
            return new Response<ConversationDetails> { Status = 401, Message = "Utilisateur non authentifié" };

        await using var tx = await context.Database.BeginTransactionAsync(ct);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Reservations\" WHERE \"Id\" = {reservationId} FOR UPDATE",
            ct
        );
        var participants = await ParticipantsAsync(reservationId);
        if (participants is null || (participants.StudentUserId != sender.Id && participants.TeacherUserId != sender.Id))
            throw new ResourceNotFoundException("Réservation introuvable");

        if (AttachmentRules.Refusal(dto.File, out var refusal) is { } message)
            return new Response<ConversationDetails> { Status = refusal, Message = message };

        var count = await context.ConversationAttachments.CountAsync(
            a => a.Conversation!.ReservationId == reservationId,
            ct
        );
        if (count >= HardCode.CHAT_ATTACHMENT_MAX_PER_RESERVATION)
            throw new BusinessConflictException(
                $"Une réservation porte au plus {HardCode.CHAT_ATTACHMENT_MAX_PER_RESERVATION} fichiers dans son chat"
            );

        var upload = await storage.Upload(dto.File, $"reservations/{reservationId}/chat", ct);
        if (upload.Status != 201 || upload.Data is null)
            return new Response<ConversationDetails> { Status = upload.Status, Message = upload.Message };

        var conversation = new Conversation
        {
            Content = dto.Content?.Trim() ?? string.Empty,
            ReservationId = reservationId,
            SenderId = sender.Id,
        };
        var attachment = new ConversationAttachment
        {
            Conversation = conversation,
            ReservationId = reservationId,
            FileName = Path.GetFileName(dto.File.FileName),
            FilePath = upload.Data,
            MimeType = StorageService.ContentTypeFor(upload.Data),
            Size = dto.File.Length,
            UploadedAt = DateTimeOffset.UtcNow,
        };
        context.Conversations.Add(conversation);
        context.ConversationAttachments.Add(attachment);

        try
        {
            await context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await storage.Delete(upload.Data, CancellationToken.None);
            throw;
        }

        var notice = new ConversationCreate
        {
            Content = conversation.Content,
            ReservationId = reservationId,
            SenderId = sender.Id,
        };
        foreach (var email in new[] { participants.StudentEmail, participants.TeacherEmail })
            if (email is not null)
                await signalR.SendMessageByUserEmail(email, nameof(MessageTypeEnum.Chat), notice);
        push.EnqueueChatMessage(conversation.Id);

        return new Response<ConversationDetails>
        {
            Status = 201,
            Message = "Fichier envoyé avec succès",
            Data = new ConversationDetails
            {
                Id = conversation.Id,
                Content = conversation.Content,
                ReservationId = reservationId,
                SenderId = sender.Id,
                CreatedAt = conversation.CreatedAt,
                Attachment = new ConversationAttachmentDetails
                {
                    Id = attachment.Id,
                    FileName = attachment.FileName,
                    MimeType = attachment.MimeType,
                    Size = attachment.Size,
                    Url = storage.Url(attachment.FilePath),
                    UploadedAt = attachment.UploadedAt,
                },
            },
        };
    }

    private sealed record Participants(Guid StudentUserId, string? StudentEmail, Guid TeacherUserId, string? TeacherEmail);

    private Task<Participants?> ParticipantsAsync(Guid reservationId) =>
        context
            .Reservations.Where(r => r.Id == reservationId)
            .Select(r => new Participants(
                r.Student!.UserId,
                r.Student!.User!.Email,
                r.Slot!.Teacher!.UserId,
                r.Slot!.Teacher!.User!.Email
            ))
            .FirstOrDefaultAsync();
}
