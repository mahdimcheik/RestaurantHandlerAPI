using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.IntegrationTests;

/// <summary>Groupes d'options de bout en bout : back-office, carte publique, commande et lecture.</summary>
[Collection(ApiCollection.Name)]
public class OptionGroupApiTests(ApiFixture api) : IAsyncLifetime
{
    public Task InitializeAsync() => api.ResetRestaurantAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json);
        return body.GetProperty("data");
    }

    private static async Task<JsonElement> OkDataAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return await DataAsync(response);
    }

    private sealed record Kebab(Guid ProductId, Guid Baguette, Guid Pita, Guid Blanche, Guid Algerienne, Guid Samourai, Guid PitaIngredient);

    private async Task<Kebab> CreateKebabAsync(HttpClient manager)
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        async Task<Guid> Ingredient(string name) =>
            (await OkDataAsync(await manager.PostAsJsonAsync("/manage/ingredients", new { name = $"{name} {suffix}" }, ApiFixture.Json)))
            .GetProperty("id").GetGuid();

        var baguette = await Ingredient("Baguette");
        var pita = await Ingredient("Pita");
        var blanche = await Ingredient("Blanche");
        var algerienne = await Ingredient("Algérienne");
        var samourai = await Ingredient("Samouraï");

        var categoryId = await api.WithDbAsync(db => db.Categories.Where(c => c.Name == "Burgers").Select(c => c.Id).SingleAsync());
        var product = await OkDataAsync(await manager.PostAsJsonAsync("/manage/products", new
        {
            categoryId, name = $"Kebab {suffix}", kind = "Dish", priceCents = 800, vatRate = 10, isActive = true,
        }, ApiFixture.Json));
        var productId = product.GetProperty("id").GetGuid();

        var details = await OkDataAsync(await manager.PutAsJsonAsync($"/manage/products/{productId}/option-groups", new
        {
            groups = new object[]
            {
                new
                {
                    label = "Pain", minChoices = 1, maxChoices = 1,
                    choices = new object[] { new { name = "Baguette", ingredientId = baguette }, new { name = "Pita", ingredientId = pita } },
                },
                new
                {
                    label = "Sauces", minChoices = 0, maxChoices = 3, freeChoices = 2, extraChoicePriceCents = 50,
                    choices = new object[]
                    {
                        new { name = "Blanche", ingredientId = blanche },
                        new { name = "Algérienne", ingredientId = algerienne },
                        new { name = "Samouraï", ingredientId = samourai },
                    },
                },
            },
        }, ApiFixture.Json));

        var groups = details.GetProperty("optionGroups");
        Guid Choice(int group, int index) => groups[group].GetProperty("choices")[index].GetProperty("id").GetGuid();
        return new Kebab(productId, Choice(0, 0), Choice(0, 1), Choice(1, 0), Choice(1, 1), Choice(1, 2), pita);
    }

    private static object OrderWith(Guid productId, params Guid[] choiceIds) => new
    {
        idempotencyKey = Guid.NewGuid().ToString("N"),
        lines = new[] { new { productId, quantity = 1, optionChoiceIds = choiceIds } },
        pickupMode = "Asap",
        paymentChoice = "OnSite",
    };

    [Fact]
    public async Task Sandwich_pain_et_sauces_de_la_carte_a_la_commande()
    {
        var manager = await api.ManagerAsync();
        var kebab = await CreateKebabAsync(manager);
        var (client, _) = await api.NewClientAsync();

        var card = await OkDataAsync(await client.GetAsync("/menu"));
        var publicProduct = card.GetProperty("categories").EnumerateArray()
            .SelectMany(c => c.GetProperty("products").EnumerateArray())
            .Single(p => p.GetProperty("id").GetGuid() == kebab.ProductId);
        Assert.Equal(["Pain", "Sauces"], publicProduct.GetProperty("optionGroups").EnumerateArray().Select(g => g.GetProperty("label").GetString()));

        var response = await client.PostAsJsonAsync("/orders",
            OrderWith(kebab.ProductId, kebab.Pita, kebab.Blanche, kebab.Algerienne, kebab.Samourai), ApiFixture.Json);

        var order = await OkDataAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(850, order.GetProperty("totalCents").GetInt32());
        var labels = order.GetProperty("items")[0].GetProperty("modifications").EnumerateArray()
            .Select(m => m.GetProperty("label").GetString());
        Assert.Equal(["Pain : Pita", "Sauces : Blanche", "Sauces : Algérienne", "Sauces : Samouraï"], labels);
    }

    [Fact]
    public async Task Sans_pain_ou_choix_inconnu_refuse()
    {
        var manager = await api.ManagerAsync();
        var kebab = await CreateKebabAsync(manager);
        var (client, _) = await api.NewClientAsync();

        var noBread = await client.PostAsJsonAsync("/orders", OrderWith(kebab.ProductId, kebab.Blanche), ApiFixture.Json);
        var unknown = await client.PostAsJsonAsync("/orders", OrderWith(kebab.ProductId, kebab.Pita, Guid.NewGuid()), ApiFixture.Json);

        Assert.Equal(HttpStatusCode.Conflict, noBread.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, unknown.StatusCode);
    }

    [Fact]
    public async Task Choix_retire_du_back_office_archive_et_snapshot_conserve()
    {
        var manager = await api.ManagerAsync();
        var kebab = await CreateKebabAsync(manager);
        var (client, _) = await api.NewClientAsync();
        var orderId = (await OkDataAsync(await client.PostAsJsonAsync("/orders", OrderWith(kebab.ProductId, kebab.Pita), ApiFixture.Json)))
            .GetProperty("id").GetGuid();

        // Pita retirée : seul le pain Baguette reste proposé.
        var groupId = await api.WithDbAsync(db => db.ProductOptionChoices.Where(c => c.Id == kebab.Baguette).Select(c => c.GroupId).SingleAsync());
        await OkDataAsync(await manager.PutAsJsonAsync($"/manage/products/{kebab.ProductId}/option-groups", new
        {
            groups = new[] { new { id = groupId, label = "Pain", minChoices = 1, maxChoices = 1, choices = new[] { new { id = kebab.Baguette, name = "Baguette" } } } },
        }, ApiFixture.Json));

        Assert.NotNull(await api.WithDbAsync(db => db.ProductOptionChoices.Where(c => c.Id == kebab.Pita).Select(c => c.ArchivedAt).SingleAsync()));
        var refused = await client.PostAsJsonAsync("/orders", OrderWith(kebab.ProductId, kebab.Pita), ApiFixture.Json);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

        var order = await OkDataAsync(await client.GetAsync($"/orders/{orderId}"));
        Assert.Equal("Pain : Pita", order.GetProperty("items")[0].GetProperty("modifications")[0].GetProperty("label").GetString());
    }

    [Fact]
    public async Task Plus_de_pain_disponible_rend_le_sandwich_indisponible_sur_la_carte()
    {
        var manager = await api.ManagerAsync();
        var kebab = await CreateKebabAsync(manager);
        var baguetteIngredient = await api.WithDbAsync(db =>
            db.ProductOptionChoices.Where(c => c.Id == kebab.Baguette).Select(c => c.IngredientId!.Value).SingleAsync());

        foreach (var ingredient in new[] { baguetteIngredient, kebab.PitaIngredient })
            (await manager.PutAsJsonAsync($"/manage/ingredients/{ingredient}/availability", new { isAvailable = false })).EnsureSuccessStatusCode();

        var card = await OkDataAsync(await manager.GetAsync("/menu"));
        var product = card.GetProperty("categories").EnumerateArray()
            .SelectMany(c => c.GetProperty("products").EnumerateArray())
            .Single(p => p.GetProperty("id").GetGuid() == kebab.ProductId);
        Assert.False(product.GetProperty("isAvailable").GetBoolean());
    }

    [Fact]
    public async Task Un_ingredient_utilise_par_un_choix_ne_s_archive_pas()
    {
        var manager = await api.ManagerAsync();
        var kebab = await CreateKebabAsync(manager);

        var response = await manager.DeleteAsync($"/manage/ingredients/{kebab.PitaIngredient}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
