namespace RestaurantHandler.Integrations.EInvoicing;

/// <summary>
/// Plateforme agréée (PA) de facturation électronique (ADR-0014). Une seule est active, choisie au
/// démarrage par <c>EINVOICING_PROVIDER</c> ; chaque adaptateur traduit <see cref="EInvoiceDocument"/>
/// dans le format de sa plateforme (Factur-X, UBL, CII). Une erreur technique (réseau, 5xx) est
/// levée en exception : la demande sera retentée.
/// </summary>
public interface IEInvoicingGateway
{
    /// <summary>Nom court enregistré sur la demande (ex. « none », « nom-de-la-plateforme »).</summary>
    string Provider { get; }

    Task<EInvoiceSubmission> SubmitAsync(EInvoiceDocument document, CancellationToken ct = default);

    Task<EInvoiceSubmission> GetStatusAsync(string externalId, CancellationToken ct = default);
}

public enum EInvoiceOutcome
{
    /// <summary>Aucune plateforme configurée : la demande reste en attente, sans erreur.</summary>
    NotConfigured,
    Submitted,
    Accepted,
    Rejected,
}

public sealed record EInvoiceSubmission(EInvoiceOutcome Outcome, string? ExternalId = null, string? Message = null);

public sealed record EInvoiceParty(
    string LegalName,
    string? Siren,
    string? Siret,
    string? VatNumber,
    string? AddressLine1,
    string? AddressLine2,
    string? PostalCode,
    string? City,
    string CountryCode
);

/// <summary>Ligne au prix TTC unitaire (suppléments compris), comme sur la commande.</summary>
public sealed record EInvoiceLine(string Name, int Quantity, int UnitPriceInclVatCents, int LineTotalInclVatCents, decimal VatRate);

public sealed record EInvoiceVatLine(decimal Rate, int NetCents, int VatCents, int GrossCents);

/// <summary>
/// Facture neutre, sans entité EF ni format de plateforme. Montants en centimes.
/// <see cref="RequestId"/> sert de clé d'idempotence auprès de la plateforme.
/// </summary>
public sealed record EInvoiceDocument(
    Guid RequestId,
    int OrderNumber,
    DateTimeOffset IssueDate,
    DateTimeOffset PaidAt,
    EInvoiceParty Seller,
    EInvoiceParty Buyer,
    IReadOnlyList<EInvoiceLine> Lines,
    IReadOnlyList<EInvoiceVatLine> Vat,
    int TotalNetCents,
    int TotalVatCents,
    int TotalGrossCents,
    string Currency = "EUR",
    // Mentions de la réforme : vente de biens (restauration à emporter), TVA exigible à l'encaissement des biens.
    string OperationCategory = "Goods",
    bool VatOnDebits = false
);

/// <summary>Aucune plateforme : les demandes restent en attente jusqu'au branchement d'une PA.</summary>
public sealed class NoEInvoicingGateway : IEInvoicingGateway
{
    public const string Name = "none";

    public string Provider => Name;

    public Task<EInvoiceSubmission> SubmitAsync(EInvoiceDocument document, CancellationToken ct = default) =>
        Task.FromResult(new EInvoiceSubmission(EInvoiceOutcome.NotConfigured, Message: "Aucune plateforme de facturation configurée."));

    public Task<EInvoiceSubmission> GetStatusAsync(string externalId, CancellationToken ct = default) =>
        Task.FromResult(new EInvoiceSubmission(EInvoiceOutcome.NotConfigured));
}
