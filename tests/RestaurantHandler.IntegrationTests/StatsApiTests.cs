using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RestaurantHandler.IntegrationTests;

[Collection(ApiCollection.Name)]
public class StatsApiTests(ApiFixture api) : IAsyncLifetime
{
    // Jeudi 03/12/2026, 12h00 à Paris : aucun autre test n'écrit ce jour-là.
    private static readonly DateTimeOffset Thursday12h = ApiFixture.Monday12h.AddDays(10);
    private const string Day = "2026-12-03";

    private SeededMenu M => api.Menu;

    public Task InitializeAsync() => api.ResetRestaurantAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json)).GetProperty("data");
    }

    private static async Task<JsonElement> CounterAsync(HttpClient manager, object line) =>
        await DataAsync(await manager.PostAsJsonAsync("/manage/orders/counter",
            new { idempotencyKey = Guid.NewGuid().ToString("N"), lines = new[] { line } }, ApiFixture.Json));

    private static async Task ActAsync(HttpClient manager, Guid id, string action, object? body = null) =>
        await DataAsync(await manager.PostAsJsonAsync($"/manage/orders/{id}/{action}", body ?? new { }, ApiFixture.Json));

    [Fact]
    public async Task Tableau_ventes_et_heures_de_pointe_ne_comptent_que_les_commandes_retirees_ou_pretes_payees()
    {
        var manager = await api.ManagerAsync();
        api.Clock.SetUtcNow(Thursday12h);

        // Suppléments par un groupe d'options facultatif, choix payant lié à l'ingrédient.
        var supplements = await DataAsync(await manager.PostAsJsonAsync("/manage/option-groups", new
        {
            label = $"Suppléments {Guid.NewGuid():N}"[..20], minChoices = 0, maxChoices = 3, freeChoices = 0,
            choices = new[] { new { name = "Cheddar", ingredientId = M.Cheddar, extraPriceCents = 80 } },
        }, ApiFixture.Json));
        var groupId = supplements.GetProperty("id").GetGuid();
        var cheddarChoice = supplements.GetProperty("choices")[0].GetProperty("id").GetGuid();
        await DataAsync(await manager.PutAsJsonAsync($"/manage/products/{M.Classic}/option-groups",
            new { optionGroups = new[] { new { optionGroupId = groupId } } }, ApiFixture.Json));

        // Retirée : Classic + cheddar, 12 min de préparation.
        var classic = await CounterAsync(manager, new
        {
            productId = M.Classic,
            quantity = 1,
            optionChoices = new[] { new { choiceId = cheddarChoice, quantity = 1 } },
        });
        var classicId = classic.GetProperty("id").GetGuid();
        await ActAsync(manager, classicId, "payments", new { method = "OnSiteCash" });
        await ActAsync(manager, classicId, "start");
        api.Clock.Advance(TimeSpan.FromMinutes(12));
        await ActAsync(manager, classicId, "ready");
        await ActAsync(manager, classicId, "complete");

        // Prête et payée par carte : comptée.
        var menuId = (await CounterAsync(manager, new
        {
            menuId = M.Menu,
            quantity = 1,
            choices = new object[] { new { slotId = M.PlatSlot, productId = M.Classic }, new { slotId = M.BoissonSlot, productId = M.Cola } },
        })).GetProperty("id").GetGuid();
        await ActAsync(manager, menuId, "start");
        await ActAsync(manager, menuId, "ready");
        await ActAsync(manager, menuId, "payments", new { method = "OnSiteCard" });

        // Prête non payée et annulée : jamais comptées.
        var unpaid = (await CounterAsync(manager, new { productId = M.Classic, quantity = 3 })).GetProperty("id").GetGuid();
        await ActAsync(manager, unpaid, "start");
        await ActAsync(manager, unpaid, "ready");
        var cancelled = (await CounterAsync(manager, new { productId = M.Cola, quantity = 1 })).GetProperty("id").GetGuid();
        await ActAsync(manager, cancelled, "cancel", new { reason = "Erreur de saisie" });

        var dashboard = await DataAsync(await manager.GetAsync($"/manage/stats/dashboard?date={Day}"));
        Assert.Equal(1030 + 1340, dashboard.GetProperty("revenueCents").GetInt32());
        Assert.Equal(2, dashboard.GetProperty("orders").GetInt32());
        Assert.Equal(1185, dashboard.GetProperty("averageBasketCents").GetInt32());
        Assert.Equal(2, dashboard.GetProperty("counter").GetProperty("orders").GetInt32());
        Assert.Equal(0, dashboard.GetProperty("online").GetProperty("orders").GetInt32());
        Assert.Equal(2370, dashboard.GetProperty("paidOnSiteCents").GetInt32());
        Assert.Equal(0, dashboard.GetProperty("paidOnlineCents").GetInt32());
        var byStatus = dashboard.GetProperty("byStatus");
        Assert.Equal(1, byStatus.GetProperty("Completed").GetInt32());
        Assert.Equal(2, byStatus.GetProperty("Ready").GetInt32());
        Assert.Equal(1, byStatus.GetProperty("Cancelled").GetInt32());

        // Le jour même est la valeur par défaut.
        Assert.Equal(2370, (await DataAsync(await manager.GetAsync("/manage/stats/dashboard"))).GetProperty("revenueCents").GetInt32());

        var sales = await DataAsync(await manager.GetAsync($"/manage/stats/products?period=Week&from={Day}"));
        Assert.Equal("2026-11-30", sales.GetProperty("from").GetString());
        var classicSales = sales.GetProperty("products").EnumerateArray().Single(p => p.GetProperty("productId").GetGuid() == M.Classic);
        Assert.Equal(1, classicSales.GetProperty("quantityAlone").GetInt32());
        Assert.Equal(1, classicSales.GetProperty("quantityInMenus").GetInt32());
        Assert.Equal(1030, classicSales.GetProperty("revenueCents").GetInt32());
        var menu = Assert.Single(sales.GetProperty("menus").EnumerateArray());
        Assert.Equal(1340, menu.GetProperty("revenueCents").GetInt32());
        var cheddar = Assert.Single(sales.GetProperty("supplements").EnumerateArray());
        Assert.Equal(M.Cheddar, cheddar.GetProperty("ingredientId").GetGuid());
        Assert.Equal(80, cheddar.GetProperty("revenueCents").GetInt32());

        var peaks = await DataAsync(await manager.GetAsync($"/manage/stats/peak-hours?period=Custom&from={Day}&to={Day}&bucketMinutes=15"));
        var bucket = Assert.Single(peaks.GetProperty("buckets").EnumerateArray());
        Assert.Equal("Thursday", bucket.GetProperty("dayOfWeek").GetString());
        Assert.Equal("12:00:00", bucket.GetProperty("start").GetString());
        Assert.Equal(2, bucket.GetProperty("orders").GetInt32());
        Assert.Equal(6, bucket.GetProperty("averagePreparationMinutes").GetDouble());

        // Recherche par numéro à la remise (US-E06-4).
        var number = classic.GetProperty("dailyNumber").GetInt32();
        var found = await DataAsync(await manager.GetAsync($"/manage/orders?date={Day}&number={number}"));
        Assert.Equal(classicId, Assert.Single(found.EnumerateArray()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Statistiques_reservees_au_Manager_et_plage_controlee()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().GetAsync("/manage/stats/dashboard")).StatusCode);
        var (client, _) = await api.NewClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/manage/stats/products")).StatusCode);

        var manager = await api.ManagerAsync();
        Assert.False((await manager.GetAsync("/manage/stats/products?period=Custom&from=2026-12-03")).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.GetAsync("/manage/stats/peak-hours?bucketMinutes=20")).StatusCode);
    }
}
