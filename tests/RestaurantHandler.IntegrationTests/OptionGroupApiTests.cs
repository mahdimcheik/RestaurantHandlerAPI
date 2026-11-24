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

    private sealed record Kebab(
        Guid ProductId, Guid PainGroup, string Pain, string Sauces,
        Guid Baguette, Guid Pita, Guid Blanche, Guid Algerienne, Guid Samourai, Guid PitaIngredient);

    /// <summary>Libellés suffixés : les groupes sont uniques et la base n'est pas vidée entre deux tests.</summary>
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

        var pain = await OkDataAsync(await manager.PostAsJsonAsync("/manage/option-groups", new
        {
            label = $"Pain {suffix}", minChoices = 1, maxChoices = 1,
            choices = new object[] { new { name = "Baguette", ingredientId = baguette }, new { name = "Pita", ingredientId = pita } },
        }, ApiFixture.Json));
        var sauces = await OkDataAsync(await manager.PostAsJsonAsync("/manage/option-groups", new
        {
            label = $"Sauces {suffix}", minChoices = 0, maxChoices = 3,
            choices = new object[]
            {
                new { name = "Blanche", ingredientId = blanche },
                new { name = "Algérienne", ingredientId = algerienne },
                new { name = "Samouraï", ingredientId = samourai },
            },
        }, ApiFixture.Json));

        var productId = await CreateProductAsync(manager, $"Kebab {suffix}");
        await LinkAsync(manager, productId, pain.GetProperty("id").GetGuid(), sauces.GetProperty("id").GetGuid());

        Guid Choice(JsonElement group, int index) => group.GetProperty("choices")[index].GetProperty("id").GetGuid();
        return new Kebab(
            productId, pain.GetProperty("id").GetGuid(), $"Pain {suffix}", $"Sauces {suffix}",
            Choice(pain, 0), Choice(pain, 1), Choice(sauces, 0), Choice(sauces, 1), Choice(sauces, 2), pita);
    }

    private async Task<Guid> CreateProductAsync(HttpClient manager, string name)
    {
        var categoryId = await api.WithDbAsync(db => db.Categories.Where(c => c.Name == "Burgers").Select(c => c.Id).SingleAsync());
        var product = await OkDataAsync(await manager.PostAsJsonAsync("/manage/products", new
        {
            categoryId, name, kind = "Dish", priceCents = 800, vatRate = 10, isActive = true,
        }, ApiFixture.Json));
        return product.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> LinkAsync(HttpClient manager, Guid productId, params Guid[] groupIds) =>
        await OkDataAsync(await manager.PutAsJsonAsync(
            $"/manage/products/{productId}/option-groups",
            new { optionGroups = groupIds.Select(optionGroupId => new { optionGroupId }) }, ApiFixture.Json));

    private static object OrderWith(Guid productId, params Guid[] choiceIds) => new
    {
        idempotencyKey = Guid.NewGuid().ToString("N"),
        lines = new[] { new { productId, quantity = 1, optionChoices = choiceIds.Select(choiceId => new { choiceId }) } },
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
        Assert.Equal([kebab.Pain, kebab.Sauces], publicProduct.GetProperty("optionGroups").EnumerateArray().Select(g => g.GetProperty("label").GetString()));

        var response = await client.PostAsJsonAsync("/orders",
            OrderWith(kebab.ProductId, kebab.Pita, kebab.Blanche, kebab.Algerienne, kebab.Samourai), ApiFixture.Json);

        var order = await OkDataAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(800, order.GetProperty("totalCents").GetInt32());
        var labels = order.GetProperty("items")[0].GetProperty("modifications").EnumerateArray()
            .Select(m => m.GetProperty("label").GetString());
        Assert.Equal(
            [$"{kebab.Pain} : Pita", $"{kebab.Sauces} : Blanche", $"{kebab.Sauces} : Algérienne", $"{kebab.Sauces} : Samouraï"],
            labels);
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
        await OkDataAsync(await manager.PutAsJsonAsync($"/manage/option-groups/{kebab.PainGroup}", new
        {
            label = kebab.Pain, minChoices = 1, maxChoices = 1, choices = new[] { new { id = kebab.Baguette, name = "Baguette" } },
        }, ApiFixture.Json));

        Assert.NotNull(await api.WithDbAsync(db => db.OptionChoices.Where(c => c.Id == kebab.Pita).Select(c => c.ArchivedAt).SingleAsync()));
        var refused = await client.PostAsJsonAsync("/orders", OrderWith(kebab.ProductId, kebab.Pita), ApiFixture.Json);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

        var order = await OkDataAsync(await client.GetAsync($"/orders/{orderId}"));
        Assert.Equal($"{kebab.Pain} : Pita", order.GetProperty("items")[0].GetProperty("modifications")[0].GetProperty("label").GetString());
    }

    [Fact]
    public async Task Plus_de_pain_disponible_rend_le_sandwich_indisponible_sur_la_carte()
    {
        var manager = await api.ManagerAsync();
        var kebab = await CreateKebabAsync(manager);
        var baguetteIngredient = await api.WithDbAsync(db =>
            db.OptionChoices.Where(c => c.Id == kebab.Baguette).Select(c => c.IngredientId!.Value).SingleAsync());

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

    [Fact]
    public async Task Un_groupe_partage_se_modifie_une_fois_pour_tous_ses_produits()
    {
        var manager = await api.ManagerAsync();
        var kebab = await CreateKebabAsync(manager);
        var wrapId = await CreateProductAsync(manager, $"Wrap {Guid.NewGuid():N}"[..11]);
        await LinkAsync(manager, wrapId, kebab.PainGroup);

        await OkDataAsync(await manager.PutAsJsonAsync($"/manage/option-groups/{kebab.PainGroup}", new
        {
            label = kebab.Pain, minChoices = 1, maxChoices = 1,
            choices = new object[] { new { id = kebab.Baguette, name = "Baguette" }, new { id = kebab.Pita, name = "Pita" }, new { name = "Galette" } },
        }, ApiFixture.Json));

        foreach (var productId in new[] { kebab.ProductId, wrapId })
        {
            var product = await OkDataAsync(await manager.GetAsync($"/manage/products/{productId}"));
            var pain = product.GetProperty("optionGroups").EnumerateArray().Single(g => g.GetProperty("id").GetGuid() == kebab.PainGroup);
            Assert.Equal(["Baguette", "Pita", "Galette"], pain.GetProperty("choices").EnumerateArray().Select(c => c.GetProperty("name").GetString()));
        }

        var group = await OkDataAsync(await manager.GetAsync($"/manage/option-groups/{kebab.PainGroup}"));
        Assert.Equal(2, group.GetProperty("productCount").GetInt32());
    }

    [Fact]
    public async Task L_ordre_des_groupes_est_propre_a_chaque_produit()
    {
        var manager = await api.ManagerAsync();
        var kebab = await CreateKebabAsync(manager);
        var saucesId = await api.WithDbAsync(db => db.OptionChoices.Where(c => c.Id == kebab.Blanche).Select(c => c.GroupId).SingleAsync());

        var details = await LinkAsync(manager, kebab.ProductId, saucesId, kebab.PainGroup);

        Assert.Equal([kebab.Sauces, kebab.Pain], details.GetProperty("optionGroups").EnumerateArray().Select(g => g.GetProperty("label").GetString()));
    }

    [Fact]
    public async Task Un_groupe_propose_ne_s_archive_pas_puis_s_archive_une_fois_retire()
    {
        var manager = await api.ManagerAsync();
        var kebab = await CreateKebabAsync(manager);
        var saucesId = await api.WithDbAsync(db => db.OptionChoices.Where(c => c.Id == kebab.Blanche).Select(c => c.GroupId).SingleAsync());

        var refused = await manager.DeleteAsync($"/manage/option-groups/{kebab.PainGroup}");
        await LinkAsync(manager, kebab.ProductId, saucesId);
        var archived = await manager.DeleteAsync($"/manage/option-groups/{kebab.PainGroup}");
        var relink = await manager.PutAsJsonAsync(
            $"/manage/products/{kebab.ProductId}/option-groups", new { optionGroups = new[] { new { optionGroupId = kebab.PainGroup } } }, ApiFixture.Json);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.True(archived.IsSuccessStatusCode, await archived.Content.ReadAsStringAsync());
        Assert.False(relink.IsSuccessStatusCode);
        var list = await OkDataAsync(await manager.GetAsync("/manage/option-groups"));
        Assert.DoesNotContain(list.EnumerateArray(), g => g.GetProperty("id").GetGuid() == kebab.PainGroup);
    }

    [Fact]
    public async Task Deux_groupes_ne_portent_pas_le_meme_libelle()
    {
        var manager = await api.ManagerAsync();
        var kebab = await CreateKebabAsync(manager);

        var response = await manager.PostAsJsonAsync("/manage/option-groups", new
        {
            label = kebab.Pain, minChoices = 0, maxChoices = 1, choices = new[] { new { name = "Seigle" } },
        }, ApiFixture.Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Pizzas_3_et_5_partagent_les_ingredients_et_acceptent_un_double()
    {
        var manager = await api.ManagerAsync();
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var group = await OkDataAsync(await manager.PostAsJsonAsync("/manage/option-groups", new
        {
            label = $"Ingrédients {suffix}", minChoices = 3, maxChoices = 3, maxPerChoice = 2,
            choices = new[] { new { name = "Jambon" }, new { name = "Œuf" }, new { name = "Olives" } },
        }, ApiFixture.Json));
        var groupId = group.GetProperty("id").GetGuid();
        Guid Choice(int index) => group.GetProperty("choices")[index].GetProperty("id").GetGuid();
        var (jambon, oeuf, olives) = (Choice(0), Choice(1), Choice(2));

        var pizza3 = await CreateProductAsync(manager, $"Pizza 3 {suffix}");
        var pizza5 = await CreateProductAsync(manager, $"Pizza 5 {suffix}");
        await LinkAsync(manager, pizza3, groupId);
        var details = await OkDataAsync(await manager.PutAsJsonAsync($"/manage/products/{pizza5}/option-groups", new
        {
            optionGroups = new[] { new { optionGroupId = groupId, rules = new { minChoices = 5, maxChoices = 5 } } },
        }, ApiFixture.Json));
        Assert.Equal(5, details.GetProperty("optionGroups")[0].GetProperty("productRules").GetProperty("minChoices").GetInt32());

        var (client, _) = await api.NewClientAsync();
        var card = await OkDataAsync(await client.GetAsync("/menu"));
        var products = card.GetProperty("categories").EnumerateArray().SelectMany(c => c.GetProperty("products").EnumerateArray()).ToList();
        int Min(Guid id) => products.Single(p => p.GetProperty("id").GetGuid() == id).GetProperty("optionGroups")[0].GetProperty("minChoices").GetInt32();
        Assert.Equal((3, 5), (Min(pizza3), Min(pizza5)));

        object Order(Guid productId, params (Guid ChoiceId, int Quantity)[] choices) => new
        {
            idempotencyKey = Guid.NewGuid().ToString("N"),
            lines = new[] { new { productId, quantity = 1, optionChoices = choices.Select(c => new { choiceId = c.ChoiceId, quantity = c.Quantity }) } },
            pickupMode = "Asap",
            paymentChoice = "OnSite",
        };

        var ok = await client.PostAsJsonAsync("/orders", Order(pizza3, (jambon, 2), (oeuf, 1)), ApiFixture.Json);
        var order = await OkDataAsync(ok);
        var mods = order.GetProperty("items")[0].GetProperty("modifications").EnumerateArray()
            .Select(m => (m.GetProperty("label").GetString(), m.GetProperty("quantity").GetInt32()));
        Assert.Equal([($"Ingrédients {suffix} : Jambon", 2), ($"Ingrédients {suffix} : Œuf", 1)], mods);

        // Une seule commande au retrait en cours par client : les autres paniers passent par le devis.
        async Task<string> QuoteAsync(Guid productId, params (Guid ChoiceId, int Quantity)[] choices) =>
            await (await client.PostAsJsonAsync("/orders/quote", Order(productId, choices), ApiFixture.Json)).Content.ReadAsStringAsync();

        Assert.Contains("Jambon entre 1 et 2 fois", await QuoteAsync(pizza3, (jambon, 3)));
        Assert.Contains("choisissez 3", await QuoteAsync(pizza3, (jambon, 2), (oeuf, 2), (olives, 1)));
        var fiveOnPizza5 = await client.PostAsJsonAsync("/orders/quote", Order(pizza5, (jambon, 2), (oeuf, 2), (olives, 1)), ApiFixture.Json);
        Assert.Equal(800, (await OkDataAsync(fiveOnPizza5)).GetProperty("totalCents").GetInt32());

        // Retirer un choix rendrait le minimum de la pizza 5 inatteignable (2 choix × 2 < 5).
        var shrink = await manager.PutAsJsonAsync($"/manage/option-groups/{groupId}", new
        {
            label = $"Ingrédients {suffix}", minChoices = 3, maxChoices = 3, maxPerChoice = 2,
            choices = new[] { new { id = jambon, name = "Jambon" }, new { id = oeuf, name = "Œuf" } },
        }, ApiFixture.Json);
        Assert.False(shrink.IsSuccessStatusCode);
        Assert.Contains("Pizza 5", await shrink.Content.ReadAsStringAsync());
    }
}
