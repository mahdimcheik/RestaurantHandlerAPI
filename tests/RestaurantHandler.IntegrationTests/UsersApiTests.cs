using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.IntegrationTests;

[Collection(ApiCollection.Name)]
public class UsersApiTests(ApiFixture api) : IAsyncLifetime
{
    private SeededMenu M => api.Menu;

    public Task InitializeAsync() => api.ResetRestaurantAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json);

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await BodyAsync(response)).GetProperty("data");
    }

    private static Task<HttpResponseMessage> SetStatusAsync(HttpClient manager, Guid userId, Guid statusId) =>
        manager.PatchAsync($"/usermanagement/users/{userId}/status?statusId={statusId}", null);

    private static async Task<List<Guid>> FilterAsync(HttpClient manager, object filter) =>
        (await DataAsync(await manager.PostAsJsonAsync("/users/filter", filter, ApiFixture.Json)))
            .EnumerateArray().Select(u => u.GetProperty("id").GetGuid()).ToList();

    private Task<Guid> ManagerIdAsync() =>
        api.WithDbAsync(db => db.Users.Where(u => u.Email == ApiFixture.ManagerEmail).Select(u => u.Id).SingleAsync());

    [Fact]
    public async Task Recherche_par_telephone_et_filtre_par_statut()
    {
        var manager = await api.ManagerAsync();
        var (_, userId) = await api.NewClientAsync();
        var phone = $"06 {Random.Shared.Next(10, 99)} {Random.Shared.Next(10, 99)} {Random.Shared.Next(10, 99)} {Random.Shared.Next(10, 99)}";
        await api.WithDbAsync(async db =>
        {
            db.Users.Single(u => u.Id == userId).PhoneNumber = phone;
            await db.SaveChangesAsync();
        });

        Assert.Contains(userId, await FilterAsync(manager, new { name = phone.Replace(" ", "") }));

        (await SetStatusAsync(manager, userId, HardCode.ACCOUNT_BANNED)).EnsureSuccessStatusCode();
        Assert.Contains(userId, await FilterAsync(manager, new { name = phone, statusId = HardCode.ACCOUNT_BANNED, row = 50 }));
        Assert.DoesNotContain(userId, await FilterAsync(manager, new { name = phone, statusId = HardCode.ACCOUNT_ACTIVE, row = 50 }));
    }

    [Fact]
    public async Task Fiche_client_avec_compteur_de_non_venues_et_commandes()
    {
        var manager = await api.ManagerAsync();
        var (client, userId) = await api.NewClientAsync();
        var placed = await client.PostAsJsonAsync("/orders", new
        {
            idempotencyKey = Guid.NewGuid().ToString("N"),
            lines = new[] { new { productId = M.Classic, quantity = 1 } },
            pickupMode = "Asap",
            paymentChoice = "OnSite",
        }, ApiFixture.Json);
        var orderId = (await DataAsync(placed)).GetProperty("id").GetGuid();
        await api.WithDbAsync(async db =>
        {
            db.Users.Single(u => u.Id == userId).NoShowCount = 1;
            await db.SaveChangesAsync();
        });

        var user = await DataAsync(await manager.GetAsync($"/users/{userId}"));
        Assert.Equal(1, user.GetProperty("noShowCount").GetInt32());

        var response = await manager.GetAsync($"/manage/customers/{userId}/orders?page=1&pageSize=10");
        var body = await BodyAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, body.GetProperty("count").GetInt32());
        Assert.Equal(orderId, body.GetProperty("data")[0].GetProperty("id").GetGuid());

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/manage/customers/{userId}/orders")).StatusCode);
    }

    [Fact]
    public async Task Un_manager_ne_peut_ecarter_ni_son_compte_ni_le_dernier_manager_actif()
    {
        var manager = await api.ManagerAsync();
        var managerId = await ManagerIdAsync();

        var self = await SetStatusAsync(manager, managerId, HardCode.ACCOUNT_BANNED);
        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);

        // Un second Manager créé par le premier reste « en attente » mais peut se connecter.
        var email = $"manager-{Guid.NewGuid():N}@it.test";
        const string password = "Manager-pass-2";
        var created = await DataAsync(await manager.PostAsJsonAsync("/users", new
        {
            firstName = "Second",
            lastName = "Manager",
            email,
            password,
            dataProcessingConsent = true,
            privacyPolicyConsent = true,
            roleId = HardCode.ROLE_MANAGER,
        }, ApiFixture.Json));
        var secondId = created.GetProperty("id").GetGuid();

        var second = api.CreateClient();
        var login = await second.PostAsJsonAsync("/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        var token = (await BodyAsync(login)).GetProperty("data").GetProperty("token").GetString();
        second.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var last = await SetStatusAsync(second, managerId, HardCode.ACCOUNT_BANNED);
        Assert.Equal(HttpStatusCode.BadRequest, last.StatusCode);
        Assert.Equal(HardCode.ACCOUNT_ACTIVE, await api.WithDbAsync(db => db.Users.Where(u => u.Id == managerId).Select(u => u.StatusId).SingleAsync()));

        // Le premier Manager restant actif, le second peut être banni : ses sessions tombent.
        (await SetStatusAsync(manager, secondId, HardCode.ACCOUNT_BANNED)).EnsureSuccessStatusCode();
        Assert.False(await api.WithDbAsync(db => db.RefreshTokens.AnyAsync(t => t.UserId == secondId)));
    }
}
