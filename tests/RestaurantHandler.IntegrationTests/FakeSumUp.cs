using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RestaurantHandler.IntegrationTests;

/// <summary>
/// Faux SumUp en mémoire, au format de l'API documentée (checkouts v0.1 en unités majeures,
/// remboursements v1.0). Les tests pilotent l'issue d'un paiement avec <see cref="Pay"/>.
/// </summary>
public sealed class FakeSumUp : HttpMessageHandler
{
    public sealed class Checkout
    {
        public required string Id { get; init; }
        public required string Reference { get; init; }
        public required decimal Amount { get; init; }
        public string Status { get; set; } = "PENDING";
        public string? TransactionId { get; set; }
        public decimal PaidAmount { get; set; }
        public string PaidCurrency { get; set; } = "EUR";
        public bool Deactivated { get; set; }
        public string? ReturnUrl { get; init; }
    }

    public ConcurrentDictionary<string, Checkout> Checkouts { get; } = new();
    public ConcurrentBag<string> Refunds { get; } = [];
    public bool FailCreate { get; set; }
    public bool FailRefund { get; set; }

    public Checkout ForReference(Guid paymentId) => Checkouts.Values.Single(c => c.Reference == paymentId.ToString("N"));

    /// <summary>Le client paie sur la page SumUp (montant différent pour simuler une anomalie).</summary>
    public void Pay(string checkoutId, decimal? amount = null, string currency = "EUR")
    {
        var c = Checkouts[checkoutId];
        c.Status = "PAID";
        c.TransactionId = Guid.NewGuid().ToString();
        c.PaidAmount = amount ?? c.Amount;
        c.PaidCurrency = currency;
    }

    public void Reset()
    {
        Checkouts.Clear();
        Refunds.Clear();
        FailCreate = false;
        FailRefund = false;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        var path = request.RequestUri!.AbsolutePath.Trim('/').Split('/');

        if (request.Method == HttpMethod.Post && path is ["v0.1", "checkouts"])
        {
            if (FailCreate)
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!;
            Assert.True(body["hosted_checkout"]!["enabled"]!.GetValue<bool>());
            Assert.Equal("EUR", body["currency"]!.GetValue<string>());
            var reference = body["checkout_reference"]!.GetValue<string>();
            if (Checkouts.Values.Any(c => c.Reference == reference))
                return Json(HttpStatusCode.Conflict, new { error_code = "DUPLICATED_CHECKOUT" });

            var checkout = new Checkout
            {
                Id = Guid.NewGuid().ToString(),
                Reference = reference,
                Amount = body["amount"]!.GetValue<decimal>(),
                ReturnUrl = body["return_url"]?.GetValue<string>(),
            };
            Checkouts[checkout.Id] = checkout;
            return Json(HttpStatusCode.Created, ToBody(checkout));
        }

        if (path is ["v0.1", "checkouts", var id])
        {
            if (!Checkouts.TryGetValue(id, out var checkout))
                return Json(HttpStatusCode.NotFound, new { error_code = "NOT_FOUND" });
            if (request.Method == HttpMethod.Get)
                return Json(HttpStatusCode.OK, ToBody(checkout));
            if (request.Method == HttpMethod.Delete)
            {
                if (checkout.Status != "PENDING")
                    return Json(HttpStatusCode.Conflict, new { error_code = "CHECKOUT_PROCESSED" });
                checkout.Status = "EXPIRED";
                checkout.Deactivated = true;
                return Json(HttpStatusCode.OK, ToBody(checkout));
            }
        }

        if (request.Method == HttpMethod.Post && path is ["v1.0", "merchants", _, "payments", var transactionId, "refunds"])
        {
            if (FailRefund)
                return Json(HttpStatusCode.Conflict, new { message = "not refundable" });
            Refunds.Add(transactionId);
            return Json(HttpStatusCode.Created, new { });
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static object ToBody(Checkout c) =>
        new
        {
            id = c.Id,
            checkout_reference = c.Reference,
            amount = c.Amount,
            currency = "EUR",
            status = c.Status,
            hosted_checkout_url = $"https://checkout.sumup.test/pay/{c.Id}",
            transactions = c.TransactionId is null
                ? Array.Empty<object>()
                : [new { id = c.TransactionId, transaction_code = "TX" + c.Id[..8], status = "SUCCESSFUL", amount = c.PaidAmount, currency = c.PaidCurrency }],
        };

    private static HttpResponseMessage Json(HttpStatusCode status, object body) =>
        new(status) { Content = JsonContent.Create(body, options: new JsonSerializerOptions()) };
}
