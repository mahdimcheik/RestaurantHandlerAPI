using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RestaurantHandler.Models;
using RestaurantHandler.Services;

namespace RestaurantHandler.IntegrationTests;

[Collection(ApiCollection.Name)]
public class PaymentApiTests(ApiFixture api) : IAsyncLifetime
{
    private SeededMenu M => api.Menu;

    public Task InitializeAsync() => api.ResetRestaurantAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json)).GetProperty("data");

    /// <summary>Commande en ligne de 2 Classic (19,00 €) ; renvoie l'id et le checkout SumUp créé.</summary>
    private async Task<(Guid OrderId, string CheckoutId, HttpClient Client)> PlaceOnlineAsync()
    {
        var (client, _) = await api.NewClientAsync();
        var response = await client.PostAsJsonAsync("/orders", new
        {
            idempotencyKey = Guid.NewGuid().ToString("N"),
            lines = new[] { new { productId = M.Classic, quantity = 2 } },
            pickupMode = "Asap",
        }, ApiFixture.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var data = await DataAsync(response);
        Assert.Equal("PendingPayment", data.GetProperty("status").GetString());
        var url = data.GetProperty("paymentUrl").GetString()!;
        Assert.StartsWith("https://checkout.sumup.test/pay/", url);
        return (data.GetProperty("id").GetGuid(), url.Split('/').Last(), client);
    }

    private Task<HttpResponseMessage> NotifyAsync(string checkoutId) =>
        api.CreateClient().PostAsJsonAsync("/payments/sumup/notifications", new { event_type = "CHECKOUT_STATUS_CHANGED", id = checkoutId });

    private Task<Order> OrderAsync(Guid id) =>
        api.WithDbAsync(db => db.Orders.Include(o => o.Payments).Include(o => o.StatusHistory).AsNoTracking().SingleAsync(o => o.Id == id));

    private async Task ReconcileAllAsync()
    {
        using var scope = api.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OnlinePaymentService>().ReconcileAllAsync();
    }

    [Fact]
    public async Task Paiement_de_bout_en_bout_confirme_la_commande_et_la_notification_rejouee_est_sans_effet()
    {
        var (id, checkoutId, client) = await PlaceOnlineAsync();
        var checkout = api.SumUp.Checkouts[checkoutId];
        Assert.Equal(19.00m, checkout.Amount);
        Assert.Equal("https://api.test/payments/sumup/notifications", checkout.ReturnUrl);

        // Une notification avant paiement ne change rien.
        Assert.Equal(HttpStatusCode.NoContent, (await NotifyAsync(checkoutId)).StatusCode);
        Assert.Equal(OrderStatus.PendingPayment, (await OrderAsync(id)).Status);

        api.SumUp.Pay(checkoutId);
        Assert.Equal(HttpStatusCode.NoContent, (await NotifyAsync(checkoutId)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await NotifyAsync(checkoutId)).StatusCode);
        await ReconcileAllAsync();

        var order = await OrderAsync(id);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.NotNull(order.PaidAt);
        Assert.Single(order.Payments, p => p.Status == PaymentStatus.Successful);
        Assert.Single(order.StatusHistory, h => h.Status == OrderStatus.Confirmed);

        var view = await DataAsync(await client.GetAsync($"/orders/{id}"));
        Assert.True(view.GetProperty("isPaid").GetBoolean());
        Assert.Equal(JsonValueKind.Null, view.GetProperty("paymentUrl").ValueKind);

        var receipt = await DataAsync(await client.GetAsync($"/orders/{id}/receipt"));
        Assert.Equal(1900, receipt.GetProperty("totalCents").GetInt32());
        Assert.Equal("SumupOnline", receipt.GetProperty("payments")[0].GetProperty("method").GetString());
        Assert.Equal("Confirmed", receipt.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, receipt.GetProperty("statusChangedAt").ValueKind);
    }

    [Fact]
    public async Task Montant_different_met_en_anomalie_sans_transition_ni_expiration()
    {
        var (id, checkoutId, _) = await PlaceOnlineAsync();

        api.SumUp.Pay(checkoutId, amount: 10.00m);
        await NotifyAsync(checkoutId);

        var order = await OrderAsync(id);
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Null(order.PaidAt);
        Assert.Contains("10,00 EUR", order.PaymentIssue);

        api.Clock.Advance(TimeSpan.FromMinutes(31));
        await ReconcileAllAsync();
        Assert.Equal(OrderStatus.PendingPayment, (await OrderAsync(id)).Status);

        var manager = await api.ManagerAsync();
        var details = await DataAsync(await manager.GetAsync($"/manage/orders/{id}"));
        Assert.False(string.IsNullOrEmpty(details.GetProperty("paymentIssue").GetString()));
    }

    [Fact]
    public async Task Notification_inconnue_ou_malformee_repond_2xx_sans_effet()
    {
        Assert.Equal(HttpStatusCode.NoContent, (await NotifyAsync("inconnu")).StatusCode);
        var other = await api.CreateClient().PostAsJsonAsync("/payments/sumup/notifications", new { event_type = "SOMETHING_ELSE" });
        Assert.Equal(HttpStatusCode.NoContent, other.StatusCode);
    }

    [Fact]
    public async Task Notification_perdue_rattrapee_par_la_reconciliation()
    {
        var (id, checkoutId, _) = await PlaceOnlineAsync();
        api.SumUp.Pay(checkoutId);

        api.Clock.Advance(TimeSpan.FromMinutes(2));
        await ReconcileAllAsync();

        Assert.Equal(OrderStatus.Confirmed, (await OrderAsync(id)).Status);
    }

    [Fact]
    public async Task Commande_non_payee_dans_le_delai_annulee_et_checkout_desactive()
    {
        var (id, checkoutId, _) = await PlaceOnlineAsync();

        api.Clock.Advance(TimeSpan.FromMinutes(29));
        await ReconcileAllAsync();
        Assert.Equal(OrderStatus.PendingPayment, (await OrderAsync(id)).Status);

        api.Clock.Advance(TimeSpan.FromMinutes(2));
        await ReconcileAllAsync();

        var order = await OrderAsync(id);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.True(api.SumUp.Checkouts[checkoutId].Deactivated);
        Assert.Equal(PaymentStatus.Cancelled, order.Payments.Single().Status);
    }

    [Fact]
    public async Task Paiement_arrive_apres_annulation_est_rembourse()
    {
        var (id, checkoutId, _) = await PlaceOnlineAsync();
        // Le client paie, le restaurant annule avant que la notification n'arrive.
        api.SumUp.Pay(checkoutId);
        var manager = await api.ManagerAsync();
        (await manager.PostAsJsonAsync($"/manage/orders/{id}/cancel", new { reason = "Client au téléphone" }, ApiFixture.Json)).EnsureSuccessStatusCode();

        var order = await OrderAsync(id);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        var payment = order.Payments.Single();
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Contains(payment.SumupTransactionId!, api.SumUp.Refunds);

        await NotifyAsync(checkoutId);
        Assert.Single(api.SumUp.Refunds);
    }

    [Fact]
    public async Task Annulation_d_une_commande_payee_rembourse_et_echec_signale()
    {
        var (id, checkoutId, client) = await PlaceOnlineAsync();
        api.SumUp.Pay(checkoutId);
        await NotifyAsync(checkoutId);

        // Le client ne peut pas annuler lui-même (ADR-0013) : il appelle le restaurant.
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/orders/{id}/cancel", null)).StatusCode);

        api.SumUp.FailRefund = true;
        var manager = await api.ManagerAsync();
        (await manager.PostAsJsonAsync($"/manage/orders/{id}/cancel", new { reason = "Demandé sur place" }, ApiFixture.Json)).EnsureSuccessStatusCode();
        var failed = await OrderAsync(id);
        Assert.Equal(PaymentStatus.Successful, failed.Payments.Single().Status);
        Assert.NotNull(failed.PaymentIssue);

        api.SumUp.FailRefund = false;
        (await manager.PostAsync($"/manage/orders/{id}/refund", null)).EnsureSuccessStatusCode();
        Assert.Equal(PaymentStatus.Refunded, (await OrderAsync(id)).Payments.Single().Status);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsync($"/manage/orders/{id}/refund", null)).StatusCode);

        // Le reçu reste disponible et dit ce qu'est devenue la commande.
        var receipt = await DataAsync(await client.GetAsync($"/orders/{id}/receipt"));
        Assert.Equal("Cancelled", receipt.GetProperty("status").GetString());
        var payment = receipt.GetProperty("payments").EnumerateArray().Single();
        Assert.Equal("Refunded", payment.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, payment.GetProperty("refundedAt").ValueKind);
    }

    [Fact]
    public async Task Echec_de_paiement_puis_nouvelle_tentative()
    {
        var (id, firstCheckout, client) = await PlaceOnlineAsync();

        var retry = await DataAsync(await client.PostAsync($"/orders/{id}/payment", null));
        var secondCheckout = retry.GetProperty("paymentUrl").GetString()!.Split('/').Last();
        Assert.NotEqual(firstCheckout, secondCheckout);
        Assert.True(api.SumUp.Checkouts[firstCheckout].Deactivated);
        Assert.False(api.SumUp.Checkouts[secondCheckout].Deactivated);
    }

    [Fact]
    public async Task SumUp_indisponible_la_commande_existe_et_le_client_peut_reessayer()
    {
        api.SumUp.FailCreate = true;
        var (client, _) = await api.NewClientAsync();
        var response = await client.PostAsJsonAsync("/orders", new
        {
            idempotencyKey = Guid.NewGuid().ToString("N"),
            lines = new[] { new { productId = M.Cola, quantity = 1 } },
            pickupMode = "Asap",
        }, ApiFixture.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await DataAsync(response);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("paymentUrl").ValueKind);

        api.SumUp.FailCreate = false;
        var retry = await DataAsync(await client.PostAsync($"/orders/{data.GetProperty("id").GetGuid()}/payment", null));
        Assert.StartsWith("https://checkout.sumup.test/", retry.GetProperty("paymentUrl").GetString());
    }

    [Fact]
    public async Task Pas_de_recu_avant_paiement()
    {
        var (id, _, client) = await PlaceOnlineAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync($"/orders/{id}/receipt")).StatusCode);
    }
}
