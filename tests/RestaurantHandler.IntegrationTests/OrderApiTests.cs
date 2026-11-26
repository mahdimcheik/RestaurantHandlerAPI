using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RestaurantHandler.Models;

namespace RestaurantHandler.IntegrationTests;

[Collection(ApiCollection.Name)]
public class OrderApiTests(ApiFixture api) : IAsyncLifetime
{
    private SeededMenu M => api.Menu;

    public Task InitializeAsync() => api.ResetRestaurantAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static string Key() => Guid.NewGuid().ToString("N");

    private object ClassicLine(int qty = 1, params object[] mods) => new { productId = M.Classic, quantity = qty, modifications = mods };

    private object Order(object[] lines, string? key = null, string mode = "Asap", DateTimeOffset? slot = null) =>
        new { idempotencyKey = key ?? Key(), lines, pickupMode = mode, slotStart = slot };

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json);
        return body.GetProperty("data");
    }

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json);

    private async Task<Guid> PlaceAsync(HttpClient client, object order)
    {
        var response = await client.PostAsJsonAsync("/orders", order, ApiFixture.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await DataAsync(response)).GetProperty("id").GetGuid();
    }

    /// <summary>Commande validée puis payée en ligne : `Confirmée`, prête pour la cuisine.</summary>
    private async Task<Guid> PlacePaidAsync(HttpClient client, object order)
    {
        var id = await PlaceAsync(client, order);
        await api.PayAsync(id);
        return id;
    }

    [Fact]
    public async Task Un_prix_envoye_par_le_client_est_ignore()
    {
        var (client, _) = await api.NewClientAsync();
        var order = new
        {
            idempotencyKey = Key(),
            totalCents = 1,
            lines = new[] { new { productId = M.Classic, quantity = 2, priceCents = 1, unitPriceCents = 1 } },
            pickupMode = "Asap",
        };

        var response = await client.PostAsJsonAsync("/orders", order, ApiFixture.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await DataAsync(response);
        Assert.Equal(1900, data.GetProperty("totalCents").GetInt32());
        Assert.Equal("PendingPayment", data.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Une_rupture_entre_le_panier_et_la_validation_refuse_sans_rien_creer()
    {
        var (client, userId) = await api.NewClientAsync();
        var quote = await client.PostAsJsonAsync("/orders/quote", new { lines = new[] { ClassicLine() } }, ApiFixture.Json);
        Assert.Equal(HttpStatusCode.OK, quote.StatusCode);

        var manager = await api.ManagerAsync();
        (await manager.PutAsJsonAsync($"/manage/ingredients/{M.Steak}/availability", new { isAvailable = false })).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/orders", Order([ClassicLine()]), ApiFixture.Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var issues = (await ProblemAsync(response)).GetProperty("issues");
        Assert.Equal(0, issues[0].GetProperty("lineIndex").GetInt32());
        Assert.Equal(0, await api.WithDbAsync(db => db.Orders.CountAsync(o => o.CustomerId == userId)));
    }

    [Fact]
    public async Task Rejeu_avec_la_meme_cle_renvoie_la_meme_commande()
    {
        var (client, userId) = await api.NewClientAsync();
        var order = Order([ClassicLine()], key: Key());

        var first = await PlaceAsync(client, order);
        var second = await PlaceAsync(client, order);

        Assert.Equal(first, second);
        Assert.Equal(1, await api.WithDbAsync(db => db.Orders.CountAsync(o => o.CustomerId == userId)));
    }

    [Fact]
    public async Task Deux_validations_simultanees_sur_la_derniere_place_une_seule_passe()
    {
        await api.WithDbAsync(async db =>
        {
            db.RestaurantSettings.Single().SlotCapacityOrders = 1;
            await db.SaveChangesAsync();
        });
        var slots = await DataAsync(await api.CreateClient().GetAsync("/restaurant/slots"));
        var lastSlot = slots[slots.GetArrayLength() - 1].GetProperty("start").GetDateTimeOffset();

        var clients = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => api.NewClientAsync()));
        var responses = await Task.WhenAll(clients.Select(c =>
            c.Client.PostAsJsonAsync("/orders", Order([ClassicLine()], mode: "Slot", slot: lastSlot), ApiFixture.Json)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));

        var after = await DataAsync(await api.CreateClient().GetAsync("/restaurant/slots"));
        Assert.DoesNotContain(after.EnumerateArray(), s => s.GetProperty("start").GetDateTimeOffset() == lastSlot);
    }

    [Fact]
    public async Task Un_client_ne_lit_pas_la_commande_d_un_autre_et_n_annule_pas_la_sienne()
    {
        var (owner, _) = await api.NewClientAsync();
        var (other, _) = await api.NewClientAsync();
        var id = await PlacePaidAsync(owner, Order([ClassicLine()]));

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/orders/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/orders/{id}")).StatusCode);

        // ADR-0013 : pour annuler, le client appelle le restaurant.
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsync($"/orders/{id}/cancel", null)).StatusCode);
        Assert.Equal("Confirmed", (await DataAsync(await owner.GetAsync($"/orders/{id}"))).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Permissions_anonyme_client_manager()
    {
        var anonymous = api.CreateClient();
        var (client, _) = await api.NewClientAsync();
        var manager = await api.ManagerAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/orders", Order([ClassicLine()]), ApiFixture.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.PostAsJsonAsync("/orders/quote", new { lines = new[] { ClassicLine() } }, ApiFixture.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/manage/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync("/orders", Order([ClassicLine()]), ApiFixture.Json)).StatusCode);
    }

    [Fact]
    public async Task Parcours_paye_en_ligne_cuisine_et_remise()
    {
        var (client, _) = await api.NewClientAsync();
        var manager = await api.ManagerAsync();
        var id = await PlacePaidAsync(client, Order([ClassicLine(1, new { type = "Replace", ingredientId = M.Tomate, substituteIngredientId = M.Cheddar })]));

        async Task<JsonElement> Act(string action, object? body = null)
        {
            var r = await manager.PostAsJsonAsync($"/manage/orders/{id}/{action}", body ?? new { }, ApiFixture.Json);
            Assert.True(r.IsSuccessStatusCode, $"{action} : {(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}");
            return await DataAsync(r);
        }

        await Act("start");
        Assert.Equal("Confirmed", (await Act("undo")).GetProperty("status").GetString());
        await Act("start");
        var ready = await Act("ready");
        Assert.True(ready.GetProperty("isPaid").GetBoolean());
        Assert.Equal(1110, ready.GetProperty("paidCents").GetInt32());

        var done = await Act("complete");
        Assert.Equal("Completed", done.GetProperty("status").GetString());
        Assert.Equal(
            ["PendingPayment", "Confirmed", "InPreparation", "Confirmed", "InPreparation", "Ready", "Completed"],
            done.GetProperty("history").EnumerateArray().Select(h => h.GetProperty("status").GetString()));

        var clientView = await DataAsync(await client.GetAsync($"/orders/{id}"));
        Assert.Equal("Completed", clientView.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Le_client_recoit_une_notification_a_chaque_etape_sauf_la_remise()
    {
        var (client, _) = await api.NewClientAsync();
        var manager = await api.ManagerAsync();
        var id = await PlacePaidAsync(client, Order([ClassicLine()]));
        foreach (var action in new[] { "start", "ready", "complete" })
            (await manager.PostAsJsonAsync($"/manage/orders/{id}/{action}", new { }, ApiFixture.Json)).EnsureSuccessStatusCode();

        var list = await DataAsync(await client.PostAsJsonAsync("/notifications", new { first = 0, row = 10 }, ApiFixture.Json));
        var items = list.EnumerateArray().ToList();
        var messages = items.Select(n => n.GetProperty("message").GetString()!).ToList();
        Assert.Equal(3, messages.Count);
        Assert.Contains(messages, m => m.Contains("confirmée, paiement reçu"));
        Assert.Contains(messages, m => m.Contains("en préparation"));
        Assert.Contains(messages, m => m.Contains("prête"));
        Assert.All(items, n =>
        {
            Assert.Equal($"/commandes/{id}", n.GetProperty("link").GetString());
            Assert.False(n.GetProperty("isSeen").GetBoolean());
        });

        var managerList = await DataAsync(await manager.PostAsJsonAsync("/notifications", new { first = 0, row = 10 }, ApiFixture.Json));
        Assert.Empty(managerList.EnumerateArray());
    }

    [Fact]
    public async Task Undo_refuse_apres_10_secondes()
    {
        var (client, _) = await api.NewClientAsync();
        var manager = await api.ManagerAsync();
        var id = await PlacePaidAsync(client, Order([ClassicLine()]));
        (await manager.PostAsync($"/manage/orders/{id}/start", null)).EnsureSuccessStatusCode();

        api.Clock.Advance(TimeSpan.FromSeconds(11));

        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsync($"/manage/orders/{id}/undo", null)).StatusCode);
    }

    [Fact]
    public async Task Options_de_validation_annoncent_le_paiement_en_ligne()
    {
        var (client, _) = await api.NewClientAsync();
        var response = await client.GetAsync("/orders/checkout-options");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await DataAsync(response)).GetProperty("onlinePaymentEnabled").GetBoolean());

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().GetAsync("/orders/checkout-options")).StatusCode);
        var manager = await api.ManagerAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/orders/checkout-options")).StatusCode);
    }

    [Fact]
    public async Task Les_actions_sur_place_n_existent_plus()
    {
        var swagger = await api.CreateClient().GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var paths = swagger.GetProperty("paths");
        foreach (var removed in new[]
        {
            "/orders/{id}/cancel", "/orders/{id}/pay-on-site", "/manage/orders/counter", "/manage/orders/{id}/accept",
            "/manage/orders/{id}/refuse", "/manage/orders/{id}/payments", "/manage/customers/{userId}/no-shows/reset",
        })
            Assert.False(paths.TryGetProperty(removed, out _), removed);
        Assert.True(paths.TryGetProperty("/manage/orders/{id}/cancel", out _));
    }

    [Fact]
    public async Task Le_refus_de_panier_est_decrit_dans_le_contrat_openapi()
    {
        var swagger = await api.CreateClient().GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var schema = swagger.GetProperty("components").GetProperty("schemas").GetProperty("CartProblemDetails");
        Assert.True(schema.GetProperty("properties").TryGetProperty("issues", out _));
        var place = swagger.GetProperty("paths").GetProperty("/orders").GetProperty("post").GetProperty("responses");
        Assert.True(place.TryGetProperty("409", out _));
        Assert.True(swagger.GetProperty("paths").TryGetProperty("/orders/checkout-options", out _));
    }

    [Fact]
    public async Task Non_venu_seulement_apres_le_delai_de_grace()
    {
        var (client, _) = await api.NewClientAsync();
        var manager = await api.ManagerAsync();
        var id = await PlacePaidAsync(client, Order([ClassicLine()]));
        foreach (var action in new[] { "start", "ready" })
            (await manager.PostAsync($"/manage/orders/{id}/{action}", null)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsync($"/manage/orders/{id}/no-show", null)).StatusCode);

        api.Clock.Advance(TimeSpan.FromMinutes(15 + 30));
        (await manager.PostAsync($"/manage/orders/{id}/no-show", null)).EnsureSuccessStatusCode();

        var clientView = await DataAsync(await client.GetAsync($"/orders/{id}"));
        Assert.Equal("NoShow", clientView.GetProperty("status").GetString());
    }

    [Fact]
    public async Task La_note_d_un_article_est_conservee_et_relue()
    {
        var (client, _) = await api.NewClientAsync();
        var line = new { productId = M.Classic, quantity = 1, note = "  Bien cuite, coupée en 8 " };
        var id = await PlaceAsync(client, Order([line, ClassicLine()]));

        var items = (await DataAsync(await client.GetAsync($"/orders/{id}"))).GetProperty("items");
        Assert.Equal("Bien cuite, coupée en 8", items[0].GetProperty("note").GetString());
        Assert.True(!items[1].TryGetProperty("note", out var none) || none.ValueKind == JsonValueKind.Null);

        var tooLong = await client.PostAsJsonAsync("/orders", Order([new { productId = M.Classic, quantity = 1, note = new string('x', 201) }]), ApiFixture.Json);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [Fact]
    public async Task Les_snapshots_ne_changent_pas_quand_la_carte_change()
    {
        var (client, _) = await api.NewClientAsync();
        var menuLine = new
        {
            menuId = M.Menu,
            quantity = 1,
            choices = new object[]
            {
                new { slotId = M.PlatSlot, productId = M.Classic, modifications = new[] { new { type = "Remove", ingredientId = M.Tomate } } },
                new { slotId = M.BoissonSlot, productId = M.Cola },
            },
        };
        var id = await PlaceAsync(client, Order([menuLine]));

        var manager = await api.ManagerAsync();
        var category = await api.WithDbAsync(db => db.Products.Where(p => p.Id == M.Classic).Select(p => p.CategoryId).SingleAsync());
        (await manager.PutAsJsonAsync($"/manage/products/{M.Classic}",
            new { categoryId = category, name = "Classic XL", kind = "Dish", priceCents = 1500, vatRate = 10 }, ApiFixture.Json)).EnsureSuccessStatusCode();

        var view = await DataAsync(await client.GetAsync($"/orders/{id}"));
        var item = view.GetProperty("items")[0];
        Assert.Equal(1340, view.GetProperty("totalCents").GetInt32());
        Assert.Equal(1340, item.GetProperty("lineTotalCents").GetInt32());
        var plat = item.GetProperty("components")[0];
        Assert.Equal("Classic", plat.GetProperty("name").GetString());
        Assert.Equal("Sans Tomate", plat.GetProperty("modifications")[0].GetProperty("label").GetString());

        (await manager.PutAsJsonAsync($"/manage/products/{M.Classic}",
            new { categoryId = category, name = "Classic", kind = "Dish", priceCents = 950, vatRate = 10 }, ApiFixture.Json)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task La_pause_bloque_la_commande_en_ligne_et_les_numeros_du_jour_se_suivent()
    {
        var manager = await api.ManagerAsync();
        (await manager.PostAsJsonAsync("/manage/restaurant/pause", new { durationMinutes = 15 })).EnsureSuccessStatusCode();

        var (client, _) = await api.NewClientAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/orders", Order([ClassicLine()]), ApiFixture.Json)).StatusCode);

        api.Clock.Advance(TimeSpan.FromMinutes(16));
        async Task<int> NumberAsync()
        {
            var r = await client.PostAsJsonAsync("/orders", Order([ClassicLine()]), ApiFixture.Json);
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
            return (await DataAsync(r)).GetProperty("dailyNumber").GetInt32();
        }

        var first = await NumberAsync();
        Assert.Equal(first + 1, await NumberAsync());

        api.Clock.SetUtcNow(ApiFixture.Monday12h.AddDays(1));
        Assert.Equal(1, await NumberAsync());
    }

    [Fact]
    public async Task Remplacement_par_n_importe_quel_ingredient_propose_au_prix_de_reference()
    {
        var manager = await api.ManagerAsync();
        var pate = await DataAsync(await manager.PostAsJsonAsync("/manage/ingredients",
            new { name = $"Pâte {Key()}"[..12], priceCents = 500, isOfferedAsReplacement = false }, ApiFixture.Json));
        var pateId = pate.GetProperty("id").GetGuid();
        var (client, _) = await api.NewClientAsync();

        var card = await DataAsync(await client.GetAsync("/menu"));
        var replacements = card.GetProperty("replacements").EnumerateArray().ToList();
        Assert.Contains(replacements, r => r.GetProperty("ingredientId").GetGuid() == M.Cheddar && r.GetProperty("priceCents").GetInt32() == 160);
        Assert.DoesNotContain(replacements, r => r.GetProperty("ingredientId").GetGuid() == pateId);

        async Task<HttpResponseMessage> QuoteAsync(Guid ingredientId, Guid substituteId) =>
            await client.PostAsJsonAsync("/orders/quote", new
            {
                lines = new[] { ClassicLine(1, new { type = "Replace", ingredientId, substituteIngredientId = substituteId }) },
            }, ApiFixture.Json);

        // Tomate (0) → cheddar (1,60) : la différence est facturée.
        var cheddar = await QuoteAsync(M.Tomate, M.Cheddar);
        Assert.Equal(950 + 160, (await DataAsync(cheddar)).GetProperty("totalCents").GetInt32());
        // Non proposé en remplacement, ou ingrédient non retirable : refusé.
        Assert.Equal(HttpStatusCode.Conflict, (await QuoteAsync(M.Tomate, pateId)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await QuoteAsync(M.Steak, M.Cheddar)).StatusCode);
    }
}
