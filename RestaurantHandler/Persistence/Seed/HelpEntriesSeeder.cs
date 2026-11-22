using System.Text.Json;
using System.Text.Json.Serialization;
using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;

namespace RestaurantHandler.Persistence.Seed;

/// <summary>
/// Premières fiches du centre d'aide (<c>help-entries.json</c>, tirées du bandeau d'aide du front),
/// créées et publiées au démarrage tant que la table est vide, archivées comprises : une fiche
/// supprimée par un admin ne revient donc jamais. Passe par <see cref="HelpService"/> pour le
/// nettoyage du HTML, le slug et la colonne de recherche.
/// </summary>
public static class HelpEntriesSeeder
{
    private const string RESOURCE = "RestaurantHandler.Persistence.Seed.help-entries.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MainContext>();
        var helpService = scope.ServiceProvider.GetRequiredService<HelpService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<HelpService>>();

        if (!await context.HelpEntries.IgnoreQueryFilters().AnyAsync())
        {
            foreach (var entry in Load())
                await CreatePublishedAsync(helpService, logger, entry);
        }
    }

    private static async Task CreatePublishedAsync(HelpService helpService, ILogger logger, HelpUpsert entry)
    {
        var created = await helpService.CreateAsync(entry);
        if (created.Data?.Id is not { } id)
        {
            logger.LogWarning("Fiche d'aide initiale non créée ({Question}) : {Message}", entry.Question, created.Message);
            return;
        }
        await helpService.SetPublishedAsync(id, true);
    }

    public static List<HelpUpsert> Load() => Load(RESOURCE);

    private static List<HelpUpsert> Load(string resource)
    {
        using var stream =
            typeof(HelpEntriesSeeder).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Ressource {resource} absente de l'assembly");
        return JsonSerializer.Deserialize<List<HelpUpsert>>(stream, JsonOptions) ?? [];
    }
}
