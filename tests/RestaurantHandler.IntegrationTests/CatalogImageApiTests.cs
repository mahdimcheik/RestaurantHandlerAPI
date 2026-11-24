using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RestaurantHandler.IntegrationTests;

/// <summary>Photos de la carte : envoi en WebP public, remplacement, retrait et refus.</summary>
[Collection(ApiCollection.Name)]
public class CatalogImageApiTests(ApiFixture api) : IAsyncLifetime
{
    public Task InitializeAsync() => api.ResetRestaurantAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<JsonElement> OkDataAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json);
        return body.GetProperty("data");
    }

    private static MultipartFormDataContent Png(int width = 40, int height = 30)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(200, 80, 20));
        using var buffer = new MemoryStream();
        image.SaveAsPng(buffer);
        return File(buffer.ToArray(), "photo.png", "image/png");
    }

    private static MultipartFormDataContent File(byte[] bytes, string name, string type)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(type);
        return new MultipartFormDataContent { { file, "file", name } };
    }

    private static async Task<Guid> CreateIngredientAsync(HttpClient manager) =>
        (await OkDataAsync(await manager.PostAsJsonAsync("/manage/ingredients",
            new { name = $"Tomate {Guid.NewGuid():N}"[..20] }, ApiFixture.Json)))
        .GetProperty("id").GetGuid();

    /// <summary>Les quatre clés d'une photo multi-tailles, à partir de celle de la plus grande.</summary>
    private static string[] Variants(string key) =>
        [.. new[] { 160, 320, 640, 1200 }.Select(w => key.Replace("-w1200.webp", $"-w{w}.webp"))];

    /// <summary>Le SDK envoie le corps en morceaux signés (aws-chunked) : le WebP commence à « RIFF ».</summary>
    private Size StoredSize(string key)
    {
        var body = api.Storage.Objects[key].Body;
        return Image.Identify(body.AsSpan(body.AsSpan().IndexOf("RIFF"u8))).Size;
    }

    /// <summary>Clé d'objet à partir de l'URL publique (S3_PUBLIC_URL = http://127.0.0.1:1/test).</summary>
    private static string KeyOf(string url) => url["http://127.0.0.1:1/test/".Length..];

    [Fact]
    public async Task Photo_d_ingredient_envoyee_en_webp_public_et_visible_sur_la_carte()
    {
        var manager = await api.ManagerAsync();
        var id = await CreateIngredientAsync(manager);

        var details = await OkDataAsync(await manager.PostAsync($"/manage/ingredients/{id}/image", Png()));

        var url = details.GetProperty("imageUrl").GetString()!;
        var key = KeyOf(url);
        Assert.StartsWith("catalog/", key);
        Assert.EndsWith("-w1200.webp", key);
        foreach (var variant in Variants(key))
        {
            var stored = api.Storage.Objects[variant];
            Assert.Equal("image/webp", stored.ContentType);
            Assert.Equal("public-read", stored.Acl);
        }

        var (client, _) = await api.NewClientAsync();
        var card = await OkDataAsync(await client.GetAsync("/menu"));
        var replacement = card.GetProperty("replacements").EnumerateArray()
            .Single(r => r.GetProperty("ingredientId").GetGuid() == id);
        Assert.Equal(url, replacement.GetProperty("imageUrl").GetString());
    }

    [Fact]
    public async Task Choix_lie_sans_photo_montre_celle_de_l_ingredient_sauf_s_il_a_la_sienne()
    {
        var manager = await api.ManagerAsync();
        var ingredientId = await CreateIngredientAsync(manager);
        var ingredientUrl = (await OkDataAsync(await manager.PostAsync($"/manage/ingredients/{ingredientId}/image", Png())))
            .GetProperty("imageUrl").GetString()!;
        var group = await OkDataAsync(await manager.PostAsJsonAsync("/manage/option-groups", new
        {
            label = $"Suppléments {Guid.NewGuid():N}"[..24], minChoices = 0, maxChoices = 1,
            choices = new object[] { new { name = "Tomate", ingredientId, extraPriceCents = 50 } },
        }, ApiFixture.Json));
        var groupId = group.GetProperty("id").GetGuid();
        var choice = group.GetProperty("choices")[0];
        var choiceId = choice.GetProperty("id").GetGuid();

        Assert.Equal(JsonValueKind.Null, choice.GetProperty("imageUrl").ValueKind);
        Assert.Equal(ingredientUrl, choice.GetProperty("ingredientImageUrl").GetString());

        var categoryId = (await OkDataAsync(await manager.GetAsync("/manage/categories"))).EnumerateArray()
            .First(c => c.GetProperty("name").GetString() == "Burgers").GetProperty("id").GetGuid();
        var productId = (await OkDataAsync(await manager.PostAsJsonAsync("/manage/products", new
        {
            categoryId, name = $"Burger {Guid.NewGuid():N}"[..20], kind = "Dish", priceCents = 800, vatRate = 10, isActive = true,
        }, ApiFixture.Json))).GetProperty("id").GetGuid();
        await OkDataAsync(await manager.PutAsJsonAsync($"/manage/products/{productId}/option-groups",
            new { optionGroups = new[] { new { optionGroupId = groupId } } }, ApiFixture.Json));

        var (client, _) = await api.NewClientAsync();
        async Task<string?> PublicChoiceImageAsync()
        {
            var card = await OkDataAsync(await client.GetAsync("/menu"));
            return card.GetProperty("categories").EnumerateArray()
                .SelectMany(c => c.GetProperty("products").EnumerateArray())
                .Single(p => p.GetProperty("id").GetGuid() == productId)
                .GetProperty("optionGroups")[0].GetProperty("choices")[0].GetProperty("imageUrl").GetString();
        }

        Assert.Equal(ingredientUrl, await PublicChoiceImageAsync());

        var choiceUrl = (await OkDataAsync(await manager.PostAsync($"/manage/option-groups/{groupId}/choices/{choiceId}/image", Png(20, 20))))
            .GetProperty("choices")[0].GetProperty("imageUrl").GetString();

        Assert.NotEqual(ingredientUrl, choiceUrl);
        Assert.Equal(choiceUrl, await PublicChoiceImageAsync());
    }

    [Fact]
    public async Task Remplacer_puis_retirer_la_photo_supprime_les_anciens_objets()
    {
        var manager = await api.ManagerAsync();
        var id = await CreateIngredientAsync(manager);

        var first = KeyOf((await OkDataAsync(await manager.PostAsync($"/manage/ingredients/{id}/image", Png())))
            .GetProperty("imageUrl").GetString()!);
        var second = KeyOf((await OkDataAsync(await manager.PostAsync($"/manage/ingredients/{id}/image", Png(20, 20))))
            .GetProperty("imageUrl").GetString()!);

        Assert.NotEqual(first, second);
        Assert.All(Variants(first), variant =>
        {
            Assert.Contains(variant, api.Storage.Deleted);
            Assert.False(api.Storage.Objects.ContainsKey(variant));
        });

        var cleared = await OkDataAsync(await manager.DeleteAsync($"/manage/ingredients/{id}/image"));

        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("imageUrl").ValueKind);
        Assert.All(Variants(second), variant => Assert.Contains(variant, api.Storage.Deleted));

        var list = await OkDataAsync(await manager.GetAsync("/manage/ingredients"));
        var row = list.EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == id);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("imageUrl").ValueKind);
    }

    [Fact]
    public async Task Chaque_variante_reduite_a_sa_taille_sans_jamais_agrandir()
    {
        var manager = await api.ManagerAsync();
        var large = await CreateIngredientAsync(manager);
        var tiny = await CreateIngredientAsync(manager);

        var key = KeyOf((await OkDataAsync(await manager.PostAsync($"/manage/ingredients/{large}/image", Png(2000, 1000))))
            .GetProperty("imageUrl").GetString()!);
        var small = KeyOf((await OkDataAsync(await manager.PostAsync($"/manage/ingredients/{tiny}/image", Png(200, 100))))
            .GetProperty("imageUrl").GetString()!);

        Assert.Equal(new Size(160, 80), StoredSize(Variants(key)[0]));
        Assert.Equal(new Size(320, 160), StoredSize(Variants(key)[1]));
        Assert.Equal(new Size(640, 320), StoredSize(Variants(key)[2]));
        Assert.Equal(new Size(1200, 600), StoredSize(Variants(key)[3]));
        Assert.Equal(new Size(160, 80), StoredSize(Variants(small)[0]));
        Assert.Equal(new Size(200, 100), StoredSize(Variants(small)[3]));
    }

    [Fact]
    public async Task Retrait_de_la_photo_d_une_categorie()
    {
        var manager = await api.ManagerAsync();
        var category = await OkDataAsync(await manager.PostAsJsonAsync("/manage/categories",
            new { name = $"Cat {Guid.NewGuid():N}"[..16] }, ApiFixture.Json));
        var id = category.GetProperty("id").GetGuid();

        var key = KeyOf((await OkDataAsync(await manager.PostAsync($"/manage/categories/{id}/image", Png())))
            .GetProperty("imageUrl").GetString()!);
        var cleared = await OkDataAsync(await manager.DeleteAsync($"/manage/categories/{id}/image"));

        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("imageUrl").ValueKind);
        Assert.All(Variants(key), variant => Assert.Contains(variant, api.Storage.Deleted));
    }

    [Fact]
    public async Task Fichier_qui_n_est_pas_une_image_refuse()
    {
        var manager = await api.ManagerAsync();
        var id = await CreateIngredientAsync(manager);

        var response = await manager.PostAsync($"/manage/ingredients/{id}/image",
            File("pas une image"u8.ToArray(), "photo.png", "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Un_client_ne_peut_ni_envoyer_ni_retirer_une_photo()
    {
        var manager = await api.ManagerAsync();
        var id = await CreateIngredientAsync(manager);
        var (client, _) = await api.NewClientAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/manage/ingredients/{id}/image", Png())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/manage/ingredients/{id}/image")).StatusCode);
    }
}
