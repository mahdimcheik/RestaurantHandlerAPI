using Microsoft.EntityFrameworkCore;
using RestaurantHandler.Contexts;
using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Integrations.EInvoicing;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Services;

/// <summary>
/// Demandes de facture électronique (US-E05-6, ADR-0014). La demande est enregistrée avec la
/// confirmation du paiement (outbox), puis transmise à la plateforme agréée par un job récurrent ;
/// un échec technique la laisse en <c>Failed</c> pour une nouvelle tentative.
/// </summary>
public class EInvoicingService(
    MainContext db,
    IEInvoicingGateway gateway,
    TimeProvider clock,
    ILogger<EInvoicingService> logger
)
{
    /// <summary>Au-delà, la demande n'est plus retentée automatiquement : un Manager la relance.</summary>
    public const int MaxAutomaticAttempts = 10;

    // ---------- Création (confirmation du paiement) ----------

    /// <summary>
    /// Demande à ajouter dans la transaction de confirmation, ou null si la commande n'en veut pas,
    /// en a déjà une, ou si le client n'est plus professionnel (avertissement journalisé).
    /// </summary>
    public async Task<InvoiceRequest?> PrepareForPaidOrderAsync(Order order)
    {
        if (!order.InvoiceRequested || await db.InvoiceRequests.AnyAsync(r => r.OrderId == order.Id))
            return null;

        var buyer = await db.BusinessProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == order.CustomerId);
        if (buyer is null)
        {
            logger.LogWarning("Facture demandée pour la commande {OrderId} mais le client n'a plus de profil professionnel.", order.Id);
            return null;
        }

        var seller = await db.RestaurantSettings.AsNoTracking().FirstAsync();
        return new InvoiceRequest
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Status = InvoiceRequestStatus.Pending,
            BuyerSiren = buyer.Siren,
            BuyerSiret = buyer.Siret,
            BuyerLegalName = buyer.LegalName,
            BuyerVatNumber = buyer.VatNumber,
            BuyerAddressLine1 = buyer.AddressLine1,
            BuyerAddressLine2 = buyer.AddressLine2,
            BuyerPostalCode = buyer.PostalCode,
            BuyerCity = buyer.City,
            BuyerCountryCode = buyer.CountryCode,
            SellerLegalName = seller.LegalName ?? seller.TradeName,
            SellerSiret = seller.Siret,
            SellerVatNumber = seller.VatNumber,
            SellerAddress = seller.Address,
            CreatedAt = clock.GetUtcNow(),
        };
    }

    // ---------- Transmission ----------

    /// <summary>Job récurrent : transmet les demandes en attente ou en échec.</summary>
    public async Task SubmitPendingAsync()
    {
        var ids = await db.InvoiceRequests
            .Where(r => (r.Status == InvoiceRequestStatus.Pending || r.Status == InvoiceRequestStatus.Failed)
                && r.Attempts < MaxAutomaticAttempts)
            .OrderBy(r => r.CreatedAt)
            .Select(r => r.Id)
            .Take(50)
            .ToListAsync();
        foreach (var id in ids)
            await SubmitAsync(id);
    }

    /// <summary>Transmet une demande ; sans effet si elle est déjà transmise ou acceptée.</summary>
    public async Task SubmitAsync(Guid requestId)
    {
        var request = await db.InvoiceRequests.FirstAsync(r => r.Id == requestId);
        if (request.Status is not (InvoiceRequestStatus.Pending or InvoiceRequestStatus.Failed))
            return;

        var document = await BuildDocumentAsync(request);
        var now = clock.GetUtcNow();
        try
        {
            var result = await gateway.SubmitAsync(document);
            if (result.Outcome == EInvoiceOutcome.NotConfigured)
                return;

            request.Attempts++;
            request.Provider = gateway.Provider;
            request.ExternalId = result.ExternalId ?? request.ExternalId;
            request.Status = result.Outcome switch
            {
                EInvoiceOutcome.Accepted => InvoiceRequestStatus.Accepted,
                EInvoiceOutcome.Rejected => InvoiceRequestStatus.Rejected,
                _ => InvoiceRequestStatus.Submitted,
            };
            request.LastError = result.Outcome == EInvoiceOutcome.Rejected ? Truncate(result.Message) : null;
            request.SubmittedAt ??= now;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Transmission de la demande de facture {RequestId} en échec.", requestId);
            request.Attempts++;
            request.Status = InvoiceRequestStatus.Failed;
            request.LastError = Truncate(ex.Message);
        }

        request.UpdatedAt = now;
        await db.SaveChangesAsync();
    }

    /// <summary>Relance par un Manager d'une demande en attente ou en échec.</summary>
    public async Task<InvoiceRequestDetails> RetryAsync(Guid requestId)
    {
        var status = await db.InvoiceRequests.Where(r => r.Id == requestId).Select(r => (InvoiceRequestStatus?)r.Status).FirstOrDefaultAsync()
            ?? throw new ResourceNotFoundException("Demande de facture introuvable.");
        if (status is not (InvoiceRequestStatus.Pending or InvoiceRequestStatus.Failed))
            throw new BusinessConflictException("Cette demande a déjà été transmise.");

        await SubmitAsync(requestId);
        db.ChangeTracker.Clear();
        return await DetailsAsync(requestId, forManager: true);
    }

    // ---------- Lectures ----------

    /// <summary>Demande d'une commande, pour son propriétaire ou un Manager ; null si aucune.</summary>
    public async Task<InvoiceRequestDetails?> GetForOrderAsync(Guid orderId, CallerIdentity caller)
    {
        var order = await db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId && (caller.IsManager || o.CustomerId == caller.UserId))
            .Select(o => new { o.OrderNumber })
            .FirstOrDefaultAsync()
            ?? throw new ResourceNotFoundException("Commande introuvable.");
        var request = await db.InvoiceRequests.AsNoTracking().FirstOrDefaultAsync(r => r.OrderId == orderId);
        return request is null ? null : InvoiceRequestDetails.From(request, order.OrderNumber, caller.IsManager);
    }

    public async Task<(List<InvoiceRequestDetails> Items, int Total)> ListAsync(InvoiceRequestFilter filter)
    {
        var query = db.InvoiceRequests.AsNoTracking();
        if (filter.Status is { } status)
            query = query.Where(r => r.Status == status);

        var total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(r => new { Request = r, r.Order!.OrderNumber })
            .ToListAsync();
        return (rows.Select(x => InvoiceRequestDetails.From(x.Request, x.OrderNumber, true)).ToList(), total);
    }

    // ---------- Interne ----------

    private async Task<InvoiceRequestDetails> DetailsAsync(Guid requestId, bool forManager)
    {
        var row = await db.InvoiceRequests.AsNoTracking()
            .Where(r => r.Id == requestId)
            .Select(r => new { Request = r, r.Order!.OrderNumber })
            .FirstAsync();
        return InvoiceRequestDetails.From(row.Request, row.OrderNumber, forManager);
    }

    private async Task<EInvoiceDocument> BuildDocumentAsync(InvoiceRequest r)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Children)
            .Include(o => o.Items).ThenInclude(i => i.Modifications)
            .FirstAsync(o => o.Id == r.OrderId);
        var topLevel = order.Items.Where(i => i.ParentItemId is null).OrderBy(i => i.CreatedAt).ToList();
        var vat = VatPolicy.Breakdown(topLevel);

        var lines = topLevel
            .Select(i =>
            {
                var total = OrderPricing.LineTotal(i);
                return new EInvoiceLine(i.NameSnapshot, i.Quantity, total / i.Quantity, total, i.VatRate);
            })
            .ToList();

        return new EInvoiceDocument(
            RequestId: r.Id,
            OrderNumber: order.OrderNumber,
            IssueDate: clock.GetUtcNow(),
            PaidAt: order.PaidAt ?? r.CreatedAt,
            Seller: new EInvoiceParty(r.SellerLegalName, SirenOf(r.SellerSiret), r.SellerSiret, r.SellerVatNumber,
                r.SellerAddress, null, null, null, "FR"),
            Buyer: new EInvoiceParty(r.BuyerLegalName, r.BuyerSiren, r.BuyerSiret, r.BuyerVatNumber,
                r.BuyerAddressLine1, r.BuyerAddressLine2, r.BuyerPostalCode, r.BuyerCity, r.BuyerCountryCode),
            Lines: lines,
            Vat: vat.Select(v => new EInvoiceVatLine(v.Rate, v.NetCents, v.VatCents, v.GrossCents)).ToList(),
            TotalNetCents: vat.Sum(v => v.NetCents),
            TotalVatCents: vat.Sum(v => v.VatCents),
            TotalGrossCents: order.TotalCents
        );
    }

    private static string? SirenOf(string? siret)
    {
        var digits = BusinessIdentityPolicy.Normalize(siret);
        return digits.Length >= 9 ? digits[..9] : null;
    }

    private static string? Truncate(string? message) =>
        message is { Length: > 1000 } ? message[..1000] : message;
}
