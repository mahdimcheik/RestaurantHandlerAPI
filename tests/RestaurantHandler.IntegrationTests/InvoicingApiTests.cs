using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RestaurantHandler.Integrations.EInvoicing;
using RestaurantHandler.Models;
using RestaurantHandler.Services;

namespace RestaurantHandler.IntegrationTests;

/// <summary>Profil professionnel et demandes de facture électronique (US-E05-6, ADR-0014).</summary>
[Collection(ApiCollection.Name)]
public class InvoicingApiTests(ApiFixture api) : IAsyncLifetime
{
    private const string Profile = "/users/me/business-profile";

    public Task InitializeAsync() => api.ResetRestaurantAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json)).GetProperty("data");

    private static object ValidProfile(string legalName = "Burgers & Co") => new
    {
        siren = "404 833 048",
        siret = "40483304800006",
        legalName,
        addressLine1 = "1 rue de la Paix",
        postalCode = "75002",
        city = "Paris",
    };

    private async Task<HttpClient> NewBusinessClientAsync()
    {
        var (client, _) = await api.NewClientAsync();
        (await client.PutAsJsonAsync(Profile, ValidProfile())).EnsureSuccessStatusCode();
        return client;
    }

    private async Task<(Guid Id, HttpResponseMessage Response)> PlaceAsync(HttpClient client, bool requestInvoice)
    {
        var response = await client.PostAsJsonAsync("/orders", new
        {
            idempotencyKey = Guid.NewGuid().ToString("N"),
            lines = new[] { new { productId = api.Menu.Classic, quantity = 2 }, new { productId = api.Menu.Cola, quantity = 1 } },
            pickupMode = "Asap",
            requestInvoice,
        }, ApiFixture.Json);
        if (response.StatusCode != HttpStatusCode.Created)
            return (Guid.Empty, response);
        return ((await DataAsync(response)).GetProperty("id").GetGuid(), response);
    }

    private Task<List<InvoiceRequest>> RequestsAsync(Guid orderId) =>
        api.WithDbAsync(db => db.InvoiceRequests.AsNoTracking().Where(r => r.OrderId == orderId).ToListAsync());

    private async Task SubmitPendingAsync()
    {
        using var scope = api.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<EInvoicingService>().SubmitPendingAsync();
    }

    [Fact]
    public async Task Profil_professionnel_cree_lu_modifie_puis_supprime()
    {
        var (client, _) = await api.NewClientAsync();
        Assert.Equal(JsonValueKind.Null, (await DataAsync(await client.GetAsync(Profile))).ValueKind);

        var created = await DataAsync(await client.PutAsJsonAsync(Profile, ValidProfile()));
        Assert.Equal("404833048", created.GetProperty("siren").GetString());
        Assert.Equal("FR83404833048", created.GetProperty("vatNumber").GetString());

        var updated = await DataAsync(await client.PutAsJsonAsync(Profile, ValidProfile("Burgers SAS")));
        Assert.Equal("Burgers SAS", updated.GetProperty("legalName").GetString());
        Assert.Equal("Burgers SAS", (await DataAsync(await client.GetAsync(Profile))).GetProperty("legalName").GetString());

        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync(Profile)).StatusCode);
        Assert.Equal(JsonValueKind.Null, (await DataAsync(await client.GetAsync(Profile))).ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(Profile)).StatusCode);
    }

    [Fact]
    public async Task Siren_invalide_ou_tva_incoherente_refuses()
    {
        var (client, _) = await api.NewClientAsync();
        var badSiren = await client.PutAsJsonAsync(Profile, new
        {
            siren = "404833049", legalName = "X", addressLine1 = "a", postalCode = "75002", city = "Paris",
        });
        Assert.Equal(HttpStatusCode.BadRequest, badSiren.StatusCode);

        var badVat = await client.PutAsJsonAsync(Profile, new
        {
            siren = "404833048", vatNumber = "FR44732829320", legalName = "X", addressLine1 = "a", postalCode = "75002", city = "Paris",
        });
        Assert.Equal(HttpStatusCode.BadRequest, badVat.StatusCode);
    }

    [Fact]
    public async Task Facture_demandee_sans_profil_professionnel_refusee()
    {
        var (client, _) = await api.NewClientAsync();
        var (_, response) = await PlaceAsync(client, requestInvoice: true);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Commande_pro_payee_cree_une_seule_demande_figee()
    {
        var client = await NewBusinessClientAsync();
        var (id, _) = await PlaceAsync(client, requestInvoice: true);

        // Rien avant le paiement.
        Assert.Empty(await RequestsAsync(id));
        Assert.Equal(JsonValueKind.Null, (await DataAsync(await client.GetAsync($"/orders/{id}/invoice-request"))).ValueKind);

        var checkoutId = await api.WithDbAsync(db => db.Payments.Where(p => p.OrderId == id).Select(p => p.SumupCheckoutId!).SingleAsync());
        await api.PayAsync(id);
        // Notification rejouée : toujours une seule demande.
        await api.CreateClient().PostAsJsonAsync("/payments/sumup/notifications", new { event_type = "CHECKOUT_STATUS_CHANGED", id = checkoutId });

        var request = Assert.Single(await RequestsAsync(id));
        Assert.Equal(InvoiceRequestStatus.Pending, request.Status);
        Assert.Equal("404833048", request.BuyerSiren);
        Assert.Equal("Burgers & Co", request.BuyerLegalName);

        // Le profil modifié après coup ne change pas la demande.
        (await client.PutAsJsonAsync(Profile, ValidProfile("Autre nom"))).EnsureSuccessStatusCode();
        Assert.Equal("Burgers & Co", Assert.Single(await RequestsAsync(id)).BuyerLegalName);

        var view = await DataAsync(await client.GetAsync($"/orders/{id}/invoice-request"));
        Assert.Equal("Pending", view.GetProperty("status").GetString());
        Assert.True((await DataAsync(await client.GetAsync($"/orders/{id}"))).GetProperty("invoiceRequested").GetBoolean());

        // Un autre client ne voit pas la demande.
        var (other, _) = await api.NewClientAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/orders/{id}/invoice-request")).StatusCode);
    }

    [Fact]
    public async Task Commande_pro_sans_case_cochee_ne_cree_pas_de_demande()
    {
        var client = await NewBusinessClientAsync();
        var (id, _) = await PlaceAsync(client, requestInvoice: false);
        await api.PayAsync(id);
        Assert.Empty(await RequestsAsync(id));
    }

    [Fact]
    public async Task Sans_plateforme_la_demande_reste_en_attente()
    {
        var client = await NewBusinessClientAsync();
        var (id, _) = await PlaceAsync(client, requestInvoice: true);
        await api.PayAsync(id);

        await SubmitPendingAsync();

        var request = Assert.Single(await RequestsAsync(id));
        Assert.Equal(InvoiceRequestStatus.Pending, request.Status);
        Assert.Equal(0, request.Attempts);
    }

    [Fact]
    public async Task Transmission_reussie_puis_echec_et_relance_manager()
    {
        var client = await NewBusinessClientAsync();
        var (ok, _) = await PlaceAsync(client, requestInvoice: true);
        await api.PayAsync(ok);
        var (ko, _) = await PlaceAsync(client, requestInvoice: true);
        await api.PayAsync(ko);

        // Succès : document complet, demande transmise.
        api.EInvoicing.Outcome = EInvoiceOutcome.Submitted;
        await api.WithDbAsync(db => db.InvoiceRequests.Where(r => r.OrderId == ko).ExecuteUpdateAsync(s => s.SetProperty(r => r.Attempts, EInvoicingService.MaxAutomaticAttempts)));
        await SubmitPendingAsync();

        var submitted = Assert.Single(await RequestsAsync(ok));
        Assert.Equal(InvoiceRequestStatus.Submitted, submitted.Status);
        Assert.Equal("fake", submitted.Provider);
        Assert.Equal(1, submitted.Attempts);
        var doc = Assert.Single(api.EInvoicing.Received, d => d.RequestId == submitted.Id);
        Assert.Equal(2150, doc.TotalGrossCents);
        Assert.Equal(doc.TotalGrossCents, doc.TotalNetCents + doc.TotalVatCents);
        Assert.Equal(2, doc.Vat.Count);
        Assert.Equal("404833048", doc.Buyer.Siren);

        // Échec technique : la relance du Manager passe la demande en échec.
        api.EInvoicing.Fail = true;
        var koId = Assert.Single(await RequestsAsync(ko)).Id;
        var manager = await api.ManagerAsync();
        var retried = await DataAsync(await manager.PostAsync($"/manage/invoice-requests/{koId}/retry", null));
        Assert.Equal("Failed", retried.GetProperty("status").GetString());
        Assert.Contains("indisponible", retried.GetProperty("lastError").GetString());

        // Une demande déjà transmise ne se relance pas.
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsync($"/manage/invoice-requests/{submitted.Id}/retry", null)).StatusCode);

        var list = await DataAsync(await manager.GetAsync("/manage/invoice-requests?status=Failed"));
        Assert.Contains(list.EnumerateArray(), r => r.GetProperty("id").GetGuid() == koId);

        // Réservé au Manager.
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/manage/invoice-requests")).StatusCode);
    }
}
