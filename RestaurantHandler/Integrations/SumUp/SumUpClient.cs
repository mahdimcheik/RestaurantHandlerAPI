using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Integrations.SumUp;

/// <summary>Checkout SumUp relu (champs utiles). Montants en unités majeures côté SumUp, convertis en centimes ici.</summary>
public sealed record SumUpCheckout(
    string Id,
    string Status,
    int AmountCents,
    string Currency,
    string? HostedCheckoutUrl,
    IReadOnlyList<SumUpTransaction> Transactions
);

public sealed record SumUpTransaction(string Id, string? TransactionCode, string Status, int AmountCents, string Currency);

public sealed class SumUpException(string message, HttpStatusCode? status = null) : Exception(message)
{
    public HttpStatusCode? Status { get; } = status;
}

/// <summary>
/// Client HTTP de l'API SumUp Online Payments (https://developer.sumup.com, lu le 2026-11-22) :
/// checkouts en v0.1 (montant décimal en euros), remboursements en v1.0 (montant en centimes).
/// </summary>
public class SumUpClient(HttpClient http)
{
    public const string StatusPending = "PENDING";
    public const string StatusPaid = "PAID";
    public const string StatusFailed = "FAILED";
    public const string StatusExpired = "EXPIRED";
    public const string TransactionSuccessful = "SUCCESSFUL";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Configure(HttpClient client)
    {
        client.BaseAddress = new Uri(EnvironmentVariables.SUMUP_API_URL.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", EnvironmentVariables.SUMUP_API_KEY);
    }

    /// <summary>Crée un checkout à page hébergée ; <paramref name="reference"/> unique (64 caractères max).</summary>
    public async Task<SumUpCheckout> CreateCheckoutAsync(
        string reference,
        int amountCents,
        string description,
        string? returnUrl,
        string? redirectUrl,
        DateTimeOffset validUntil,
        CancellationToken ct = default
    )
    {
        var body = new CreateCheckoutBody(
            reference,
            amountCents / 100m,
            "EUR",
            EnvironmentVariables.SUMUP_MERCHANT_CODE,
            description,
            returnUrl,
            redirectUrl,
            validUntil.UtcDateTime,
            new HostedCheckoutBody(true)
        );
        using var response = await http.PostAsJsonAsync("v0.1/checkouts", body, Json, ct);
        return ToCheckout(await ReadAsync<CheckoutBody>(response, "création du checkout", ct));
    }

    public async Task<SumUpCheckout> GetCheckoutAsync(string checkoutId, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"v0.1/checkouts/{Uri.EscapeDataString(checkoutId)}", ct);
        return ToCheckout(await ReadAsync<CheckoutBody>(response, "lecture du checkout", ct));
    }

    /// <summary>Faux si le checkout est déjà traité (409 CHECKOUT_PROCESSED) : il faut alors le relire.</summary>
    public async Task<bool> DeactivateCheckoutAsync(string checkoutId, CancellationToken ct = default)
    {
        using var response = await http.DeleteAsync($"v0.1/checkouts/{Uri.EscapeDataString(checkoutId)}", ct);
        if (response.StatusCode is HttpStatusCode.Conflict)
            return false;
        if (response.StatusCode is HttpStatusCode.NotFound)
            return true;
        await EnsureAsync(response, "désactivation du checkout", ct);
        return true;
    }

    /// <summary>Remboursement total (corps vide) d'une transaction identifiée par son UUID.</summary>
    public async Task RefundAsync(string transactionId, CancellationToken ct = default)
    {
        var path = $"v1.0/merchants/{Uri.EscapeDataString(EnvironmentVariables.SUMUP_MERCHANT_CODE)}/payments/{Uri.EscapeDataString(transactionId)}/refunds";
        using var response = await http.PostAsJsonAsync(path, new { }, Json, ct);
        await EnsureAsync(response, "remboursement", ct);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, string action, CancellationToken ct)
    {
        await EnsureAsync(response, action, ct);
        return await response.Content.ReadFromJsonAsync<T>(Json, ct)
            ?? throw new SumUpException($"SumUp : réponse vide ({action}).", response.StatusCode);
    }

    private static async Task EnsureAsync(HttpResponseMessage response, string action, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;
        var detail = await response.Content.ReadAsStringAsync(ct);
        throw new SumUpException(
            $"SumUp : échec {action} ({(int)response.StatusCode}) {detail[..Math.Min(detail.Length, 300)]}",
            response.StatusCode
        );
    }

    private static int ToCents(decimal amount) => (int)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    private static SumUpCheckout ToCheckout(CheckoutBody b) =>
        new(
            b.Id ?? throw new SumUpException("SumUp : checkout sans identifiant."),
            b.Status ?? StatusPending,
            ToCents(b.Amount),
            b.Currency ?? "",
            b.HostedCheckoutUrl,
            (b.Transactions ?? [])
                .Select(t => new SumUpTransaction(t.Id ?? "", t.TransactionCode, t.Status ?? "", ToCents(t.Amount), t.Currency ?? ""))
                .ToList()
        );

    private sealed record HostedCheckoutBody(bool Enabled);

    private sealed record CreateCheckoutBody(
        string CheckoutReference,
        decimal Amount,
        string Currency,
        string MerchantCode,
        string Description,
        string? ReturnUrl,
        string? RedirectUrl,
        DateTime ValidUntil,
        HostedCheckoutBody HostedCheckout
    );

    private sealed record TransactionBody(string? Id, string? TransactionCode, string? Status, decimal Amount, string? Currency);

    private sealed record CheckoutBody(
        string? Id,
        string? Status,
        decimal Amount,
        string? Currency,
        string? HostedCheckoutUrl,
        List<TransactionBody>? Transactions
    );
}
