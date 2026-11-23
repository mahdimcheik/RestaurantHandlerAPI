using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using RestaurantHandler.Integrations.SumUp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Testcontainers.PostgreSql;

namespace RestaurantHandler.IntegrationTests;

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}

/// <summary>
/// Une base PostgreSQL jetable (Testcontainers) et l'API complète par-dessus. Toutes les variables
/// sensibles sont fixées ici : le .env local (NoClobber) ne peut pas les remplacer, donc aucun
/// appel au stockage ou au SMTP réels. Horloge figée : lundi 23/11/2026, 12h00 à Paris.
/// </summary>
public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly DateTimeOffset Monday12h = new(2026, 11, 23, 11, 0, 0, TimeSpan.Zero);
    public const string ManagerEmail = "manager@it.test";
    public const string ManagerPassword = "Manager-pass-1";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17").Build();

    public TestClock Clock { get; } = new(Monday12h);
    public FakeSumUp SumUp { get; } = new();
    public SeededMenu Menu { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        var cs = new Npgsql.NpgsqlConnectionStringBuilder(_db.GetConnectionString());
        var env = new Dictionary<string, string>
        {
            ["DB_HOST"] = cs.Host!,
            ["DB_PORT"] = cs.Port.ToString(),
            ["DB_NAME"] = cs.Database!,
            ["DB_USER"] = cs.Username!,
            ["DB_PASSWORD"] = cs.Password!,
            ["JWT_KEY"] = "integration-tests-jwt-key-0123456789-abcdefghijklmnop",
            ["ADMIN_EMAIL"] = ManagerEmail,
            ["ADMIN_PASSWORD"] = ManagerPassword,
            ["S3_UPLOAD_URL"] = "http://127.0.0.1:1",
            ["S3_PUBLIC_URL"] = "http://127.0.0.1:1/test",
            ["S3_BUCKET"] = "test",
            ["S3_ACCESS_KEY"] = "test",
            ["S3_SECRET_KEY"] = "test",
            ["SMTP_HOST"] = "127.0.0.1",
            ["SMTP_PORT"] = "1",
            ["GOOGLE_CLIENT_ID"] = "test",
            ["GOOGLE_CLIENT_SECRET"] = "test",
            ["RATE_LIMITING_ENABLED"] = "false",
            ["VAPID_PUBLIC_KEY"] = "",
            ["VAPID_PRIVATE_KEY"] = "",
            ["SUMUP_API_URL"] = "https://sumup.test",
            ["SUMUP_API_KEY"] = "sup_sk_test_integration",
            ["SUMUP_MERCHANT_CODE"] = "MTEST",
            ["API_BACK_URL"] = "https://api.test",
            ["API_FRONT_URL"] = "https://app.test",
        };
        foreach (var (key, value) in env)
            Environment.SetEnvironmentVariable(key, value);

        _ = Server; // démarre l'hôte : migrations + Manager initial
        Menu = await SeedAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddHttpClient<SumUpClient>().ConfigurePrimaryHttpMessageHandler(() => SumUp);
        });
    }

    public async Task<T> WithDbAsync<T>(Func<MainContext, Task<T>> work)
    {
        using var scope = Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<MainContext>());
    }

    public Task WithDbAsync(Func<MainContext, Task> work) =>
        WithDbAsync(async db =>
        {
            await work(db);
            return 0;
        });

    public async Task<HttpClient> ManagerAsync() => await LoginAsync(ManagerEmail, ManagerPassword);

    /// <summary>Nouveau compte Client confirmé, connecté.</summary>
    public async Task<(HttpClient Client, Guid UserId)> NewClientAsync()
    {
        var email = $"client-{Guid.NewGuid():N}@it.test";
        const string password = "Client-pass-1";
        using (var scope = Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<UserApp>>();
            var user = new UserApp
            {
                Email = email,
                UserName = email,
                FirstName = "Cli",
                LastName = "Ent",
                EmailConfirmed = true,
                StatusId = HardCode.ACCOUNT_ACTIVE,
                DataProcessingConsent = true,
                PrivacyPolicyConsent = true,
            };
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            await users.AddToRoleAsync(user, nameof(RoleEnum.Client));
        }

        var client = await LoginAsync(email, password);
        var id = await WithDbAsync(db => Task.FromResult(db.Users.Single(u => u.Email == email).Id));
        return (client, id);
    }

    private async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Carte minimale + ouverture tous les jours 00h00–23h59, créneaux de 15 min, 5 commandes.</summary>
    private Task<SeededMenu> SeedAsync() =>
        WithDbAsync(async db =>
        {
            var burgers = new Category { Name = "Burgers" };
            var steak = new Ingredient { Name = "Steak" };
            var tomate = new Ingredient { Name = "Tomate" };
            var cheddar = new Ingredient { Name = "Cheddar", PriceCents = 160 };
            var classic = new Product { Category = burgers, Name = "Classic", Kind = ProductKind.Dish, PriceCents = 950, VatRate = 10 };
            var cola = new Product { Category = burgers, Name = "Cola", Kind = ProductKind.Drink, PriceCents = 250, VatRate = 5.5m };
            db.AddRange(burgers, steak, tomate, cheddar, classic, cola);
            await db.SaveChangesAsync();

            db.ProductIngredients.AddRange(
                new ProductIngredient { ProductId = classic.Id, IngredientId = steak.Id, IsRemovable = false },
                new ProductIngredient { ProductId = classic.Id, IngredientId = tomate.Id, IsRemovable = true });

            var menu = new Menu { Name = "Menu Classic", PriceCents = 1290, VatRate = 10 };
            var plat = new MenuSlot { Menu = menu, Label = "Plat", Position = 0, MinChoices = 1, MaxChoices = 1 };
            var boisson = new MenuSlot { Menu = menu, Label = "Boisson", Position = 1, MinChoices = 1, MaxChoices = 1 };
            db.AddRange(menu, plat, boisson);
            await db.SaveChangesAsync();
            db.MenuSlotOptions.AddRange(
                new MenuSlotOption { SlotId = plat.Id, ProductId = classic.Id },
                new MenuSlotOption { SlotId = boisson.Id, ProductId = cola.Id, ExtraPriceCents = 50 });

            db.OpeningHours.AddRange(Enum.GetValues<DayOfWeek>()
                .Select(d => new OpeningHours { DayOfWeek = d, Start = new TimeOnly(0, 0), End = new TimeOnly(23, 59) }));
            await db.SaveChangesAsync();

            return new SeededMenu(classic.Id, cola.Id, menu.Id, plat.Id, boisson.Id, steak.Id, tomate.Id, cheddar.Id);
        });

    /// <summary>Remet les paramètres modifiés par un test à leurs valeurs de départ.</summary>
    public Task ResetRestaurantAsync() =>
        WithDbAsync(async db =>
        {
            Clock.SetUtcNow(Monday12h);
            SumUp.Reset();
            var s = db.RestaurantSettings.Single();
            s.IsPaused = false;
            s.PausedUntil = null;
            s.SlotCapacityOrders = 5;
            foreach (var i in db.Ingredients)
                i.IsAvailable = true;
            foreach (var p in db.Products)
                p.IsAvailable = true;
            await db.SaveChangesAsync();
            await Services.GetRequiredService<Microsoft.Extensions.Caching.Hybrid.HybridCache>().RemoveByTagAsync(CacheKeys.ScheduleTag);
        });
}

public sealed record SeededMenu(
    Guid Classic,
    Guid Cola,
    Guid Menu,
    Guid PlatSlot,
    Guid BoissonSlot,
    Guid Steak,
    Guid Tomate,
    Guid Cheddar
);
