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
        Assert.EndsWith(".webp", key);
        var stored = api.Storage.Objects[key];
        Assert.Equal("image/webp", stored.ContentType);
        Assert.Equal("public-read", stored.Acl);

        var (client, _) = await api.NewClientAsync();
        var card = await OkDataAsync(await client.GetAsync("/menu"));
        var replacement = card.GetProperty("replacements").EnumerateArray()
            .Single(r => r.GetProperty("ingredientId").GetGuid() == id);
        Assert.Equal(url, replacement.GetProperty("imageUrl").GetString());
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
        Assert.Contains(first, api.Storage.Deleted);
        Assert.False(api.Storage.Objects.ContainsKey(first));

        var cleared = await OkDataAsync(await manager.DeleteAsync($"/manage/ingredients/{id}/image"));

        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("imageUrl").ValueKind);
        Assert.Contains(second, api.Storage.Deleted);

        var list = await OkDataAsync(await manager.GetAsync("/manage/ingredients"));
        var row = list.EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == id);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("imageUrl").ValueKind);
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
        Assert.Contains(key, api.Storage.Deleted);
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
