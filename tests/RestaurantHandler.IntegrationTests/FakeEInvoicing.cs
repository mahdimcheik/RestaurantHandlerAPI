using System.Collections.Concurrent;
using RestaurantHandler.Integrations.EInvoicing;

namespace RestaurantHandler.IntegrationTests;

/// <summary>
/// Fausse plateforme agréée : par défaut « non configurée », comme en production tant qu'aucune
/// plateforme n'est branchée. Les tests choisissent l'issue de la transmission.
/// </summary>
public sealed class FakeEInvoicing : IEInvoicingGateway
{
    public EInvoiceOutcome Outcome { get; set; } = EInvoiceOutcome.NotConfigured;
    public bool Fail { get; set; }
    public ConcurrentBag<EInvoiceDocument> Received { get; } = [];

    public string Provider => "fake";

    public Task<EInvoiceSubmission> SubmitAsync(EInvoiceDocument document, CancellationToken ct = default)
    {
        if (Fail)
            throw new HttpRequestException("plateforme indisponible");
        Received.Add(document);
        return Task.FromResult(new EInvoiceSubmission(Outcome, Outcome == EInvoiceOutcome.NotConfigured ? null : $"PA-{document.OrderNumber}"));
    }

    public Task<EInvoiceSubmission> GetStatusAsync(string externalId, CancellationToken ct = default) =>
        Task.FromResult(new EInvoiceSubmission(Outcome, externalId));

    public void Reset()
    {
        Outcome = EInvoiceOutcome.NotConfigured;
        Fail = false;
        Received.Clear();
    }
}
