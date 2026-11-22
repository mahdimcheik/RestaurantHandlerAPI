using RestaurantHandler.Contexts;
using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;

namespace RestaurantHandler.Services;

/// <summary>
/// Produces and stores the PDF invoice (facture) for a paid order. The bill is
/// composed on the first request, stored as a private object, and recorded as a
/// <see cref="Document"/> (keyed by OrderId). Later requests return a fresh signed link
/// to the stored copy — the invoice is immutable once issued.
/// </summary>
public class FacturationService(MainContext context, StorageService storage)
{
    public async Task<Response<FileInfoResponse>> GetOrCreateInvoiceAsync(
        Guid orderId,
        CallerIdentity? principals
    )
    {
        var user = CheckUser.GetUser(principals, context);
        if (user is null)
        {
            return new Response<FileInfoResponse>
            {
                Status = 401,
                Message = "Utilisateur non authentifié",
                Data = null,
            };
        }

        var order = await context
            .Orders.Where(o =>
                o.Id == orderId && o.StudentId == user.Id && o.ArchivedAt == null
            )
            .Include(o => o.Student)
            .ThenInclude(s => s!.User)
            .ThenInclude(u => u!.Addresses.Where(a => a.ArchivedAt == null))
            .Include(o => o.Teacher)
            .ThenInclude(t => t!.User)
            .ThenInclude(u => u!.Addresses.Where(a => a.ArchivedAt == null))
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .ThenInclude(r => r.Slot)
            .Include(o => o.Reservations.Where(r => r.ArchivedAt == null))
            .ThenInclude(r => r.Cursus)
            .FirstOrDefaultAsync();

        if (order is null)
        {
            return new Response<FileInfoResponse>
            {
                Status = 404,
                Message = "Commande introuvable",
                Data = null,
            };
        }

        if (order.StatusId != HardCode.STATUS_ORDER_PAID)
        {
            return new Response<FileInfoResponse>
            {
                Status = 400,
                Message = "La facture n'est disponible qu'après le paiement de la commande",
                Data = null,
            };
        }

        // Idempotent fast path: an invoice already exists for this order.
        var document = await context.Documents.FirstOrDefaultAsync(d =>
            d.OrderId == order.Id
        );
        if (document is not null && StorageService.IsValidKey(document.FilePath))
        {
            return await SignedLinkAsync(document, "Facture récupérée avec succès");
        }

        // First request: compose the PDF, store it, record the Document.
        var pdfBytes = new InvoiceDocument(order).GeneratePdf();

        var stored = await storage.UploadBytes(
            pdfBytes,
            $"invoices/{order.Id}",
            ".pdf",
            CancellationToken.None
        );
        if (stored.Data is null)
        {
            return new Response<FileInfoResponse> { Status = stored.Status, Message = stored.Message };
        }

        if (document is null)
        {
            document = new RestaurantHandler.Models.Document
            {
                StudentId = order.StudentId,
                OrderId = order.Id,
            };
            context.Documents.Add(document);
        }

        document.FileName = $"{order.OrderNumber}.pdf";
        document.FilePath = stored.Data;
        document.MimeType = StorageService.ContentTypeFor(stored.Data);
        document.Size = pdfBytes.LongLength;
        document.UploadedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();

        return await SignedLinkAsync(document, "Facture générée avec succès");
    }

    private async Task<Response<FileInfoResponse>> SignedLinkAsync(
        RestaurantHandler.Models.Document document,
        string message
    )
    {
        return new Response<FileInfoResponse>
        {
            Status = 200,
            Message = message,
            Data = new FileInfoResponse { Name = document.FileName, Url = storage.Url(document.FilePath)! },
        };
    }
}
