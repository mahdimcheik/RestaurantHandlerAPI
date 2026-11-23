using Microsoft.EntityFrameworkCore;
using RestaurantHandler.Contexts;
using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Models;

namespace RestaurantHandler.Services;

/// <summary>
/// Groupes d'options partagés entre produits (US-E02-9). Archivés, jamais supprimés : les lignes
/// de commande pointent vers leurs choix.
/// </summary>
public class OptionGroupService(MainContext db, CatalogImageService images, CatalogChangeNotifier changes)
{
    public async Task<List<OptionGroupDetails>> ListAsync(bool includeArchived)
    {
        var groups = await WithChoices(db.OptionGroups.AsNoTracking())
            .Where(g => includeArchived || g.ArchivedAt == null)
            .OrderBy(g => g.ArchivedAt != null).ThenBy(g => g.Label)
            .ToListAsync();
        var counts = await CountProductsAsync(groups.Select(g => g.Id).ToList());

        return groups.Select(g => ToDetails(g, images, productCount: counts.GetValueOrDefault(g.Id))).ToList();
    }

    public async Task<OptionGroupDetails> GetAsync(Guid id)
    {
        var group = await WithChoices(db.OptionGroups.AsNoTracking()).FirstOrDefaultAsync(g => g.Id == id)
            ?? throw new ResourceNotFoundException("Groupe d'options introuvable.");
        var counts = await CountProductsAsync([id]);

        return ToDetails(group, images, productCount: counts.GetValueOrDefault(id));
    }

    public async Task<OptionGroupDetails> CreateAsync(OptionGroupWrite dto)
    {
        var group = new OptionGroup();
        await ApplyAsync(group, dto);
        db.OptionGroups.Add(group);
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();

        return await GetAsync(group.Id);
    }

    /// <summary>
    /// Met à jour le groupe pour tous les produits qui le proposent. Les choix existants sont mis
    /// à jour sur place ; ceux absents de la requête sont archivés.
    /// </summary>
    public async Task<OptionGroupDetails> UpdateAsync(Guid id, OptionGroupWrite dto)
    {
        var group = await db.OptionGroups
            .Include(g => g.Choices.Where(c => c.ArchivedAt == null))
            .FirstOrDefaultAsync(g => g.Id == id && g.ArchivedAt == null)
            ?? throw new ResourceNotFoundException("Groupe d'options introuvable.");

        await ApplyAsync(group, dto);
        group.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();

        return await GetAsync(id);
    }

    /// <summary>Refusé tant qu'un produit non archivé le propose.</summary>
    public async Task ArchiveAsync(Guid id)
    {
        var group = await db.OptionGroups
            .Include(g => g.Choices.Where(c => c.ArchivedAt == null))
            .FirstOrDefaultAsync(g => g.Id == id && g.ArchivedAt == null)
            ?? throw new ResourceNotFoundException("Groupe d'options introuvable.");

        var users = await db.ProductOptionGroups
            .Where(l => l.OptionGroupId == id && l.Product!.ArchivedAt == null)
            .OrderBy(l => l.Product!.Name)
            .Select(l => l.Product!.Name)
            .ToListAsync();
        if (users.Count > 0)
            throw new BusinessConflictException(
                $"Groupe proposé sur {users.Count} produit(s) : {string.Join(", ", users.Take(5))}. "
                + "Retirez-le de ces produits avant de l'archiver.");

        var now = DateTimeOffset.UtcNow;
        group.ArchivedAt = now;
        foreach (var choice in group.Choices)
            choice.ArchivedAt = now;
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();
    }

    public async Task<OptionGroupDetails> SetImageAsync(Guid id, IFormFile file, CancellationToken ct)
    {
        var group = await db.OptionGroups.FirstOrDefaultAsync(g => g.Id == id && g.ArchivedAt == null, ct)
            ?? throw new ResourceNotFoundException("Groupe d'options introuvable.");

        await ReplaceImageAsync(group.ImageUrl, key => group.ImageUrl = key, () => group.UpdatedAt = DateTimeOffset.UtcNow, file, ct);
        return await GetAsync(id);
    }

    public async Task<OptionGroupDetails> SetChoiceImageAsync(Guid id, Guid choiceId, IFormFile file, CancellationToken ct)
    {
        var choice = await db.OptionChoices
            .FirstOrDefaultAsync(c => c.Id == choiceId && c.GroupId == id && c.ArchivedAt == null
                && c.Group!.ArchivedAt == null, ct)
            ?? throw new ResourceNotFoundException("Choix introuvable.");

        await ReplaceImageAsync(choice.ImageUrl, key => choice.ImageUrl = key, () => choice.UpdatedAt = DateTimeOffset.UtcNow, file, ct);
        return await GetAsync(id);
    }

    /// <summary>Détail d'un groupe ; <paramref name="position"/> et <paramref name="productRules"/> le situent dans un produit.</summary>
    public static OptionGroupDetails ToDetails(
        OptionGroup g,
        CatalogImageService images,
        int position = 0,
        int productCount = 0,
        OptionGroupRules? productRules = null
    ) =>
        new()
        {
            Id = g.Id,
            Label = g.Label,
            ImageUrl = images.Url(g.ImageUrl),
            Position = position,
            MinChoices = g.MinChoices,
            MaxChoices = g.MaxChoices,
            FreeChoices = g.FreeChoices,
            ExtraChoicePriceCents = g.ExtraChoicePriceCents,
            MaxPerChoice = g.MaxPerChoice,
            ProductRules = productRules,
            IsArchived = g.ArchivedAt != null,
            ProductCount = productCount,
            Choices = g.Choices
                .Where(c => c.ArchivedAt == null)
                .OrderBy(c => c.Position)
                .Select(c => new OptionChoiceDetails
                {
                    Id = c.Id,
                    Name = c.Name,
                    ImageUrl = images.Url(c.ImageUrl),
                    IngredientId = c.IngredientId,
                    ExtraPriceCents = c.ExtraPriceCents,
                    Position = c.Position,
                    IsAvailable = AvailabilityPolicy.IsOptionChoiceOffered(c),
                })
                .ToList(),
        };

    private async Task ApplyAsync(OptionGroup group, OptionGroupWrite dto)
    {
        var label = dto.Label.Trim();
        var lowered = label.ToLower();
        if (await db.OptionGroups.AnyAsync(g => g.ArchivedAt == null && g.Id != group.Id && g.Label.ToLower() == lowered))
            throw new BusinessConflictException($"Un groupe d'options « {label} » existe déjà.");

        var ingredientIds = dto.Choices.Where(c => c.IngredientId is not null).Select(c => c.IngredientId!.Value).ToHashSet();
        var known = await db.Ingredients.CountAsync(i => ingredientIds.Contains(i.Id) && i.ArchivedAt == null);
        if (known != ingredientIds.Count)
            throw new BusinessRuleException("Un choix référence un ingrédient introuvable ou archivé.");

        group.Label = label;
        group.MinChoices = dto.MinChoices;
        group.MaxChoices = dto.MaxChoices;
        group.FreeChoices = dto.FreeChoices ?? dto.MaxChoices;
        group.ExtraChoicePriceCents = dto.ExtraChoicePriceCents;
        group.MaxPerChoice = dto.MaxPerChoice;

        // Les produits qui redéfinissent le minimum doivent pouvoir encore l'atteindre.
        var capacity = dto.Choices.Count * dto.MaxPerChoice;
        var blocked = await db.ProductOptionGroups
            .Where(l => l.OptionGroupId == group.Id && l.MinChoices > capacity && l.Product!.ArchivedAt == null)
            .OrderBy(l => l.Product!.Name)
            .Select(l => l.Product!.Name)
            .ToListAsync();
        if (blocked.Count > 0)
            throw new BusinessRuleException(
                $"« {label} » propose moins de choix que le minimum demandé par : {string.Join(", ", blocked.Take(5))}.");

        var now = DateTimeOffset.UtcNow;
        var choices = group.Choices.Where(c => c.ArchivedAt == null).ToDictionary(c => c.Id);
        var kept = dto.Choices.Where(c => c.Id is not null).Select(c => c.Id!.Value).ToHashSet();
        if (!kept.IsSubsetOf(choices.Keys))
            throw new BusinessRuleException($"« {label} » : un choix n'appartient pas à ce groupe.");
        foreach (var removed in choices.Values.Where(c => !kept.Contains(c.Id)))
            removed.ArchivedAt = now;

        for (var position = 0; position < dto.Choices.Count; position++)
        {
            var write = dto.Choices[position];
            OptionChoice choice;
            if (write.Id is { } choiceId)
            {
                choice = choices[choiceId];
                choice.UpdatedAt = now;
            }
            else
            {
                choice = new OptionChoice();
                group.Choices.Add(choice);
            }

            choice.Name = write.Name.Trim();
            choice.IngredientId = write.IngredientId;
            choice.ExtraPriceCents = write.ExtraPriceCents;
            choice.Position = position;
        }
    }

    private async Task ReplaceImageAsync(string? previous, Action<string> set, Action touch, IFormFile file, CancellationToken ct)
    {
        var key = await images.StoreAsync(file, ct);
        set(key);
        touch();
        await db.SaveChangesAsync(ct);
        await images.DeleteAsync(previous);
        await changes.CatalogChangedAsync();
    }

    /// <summary>Produits non archivés qui proposent chaque groupe.</summary>
    private Task<Dictionary<Guid, int>> CountProductsAsync(List<Guid> groupIds) =>
        db.ProductOptionGroups
            .Where(l => groupIds.Contains(l.OptionGroupId) && l.Product!.ArchivedAt == null)
            .GroupBy(l => l.OptionGroupId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count);

    private static IQueryable<OptionGroup> WithChoices(IQueryable<OptionGroup> query) =>
        query.Include(g => g.Choices.Where(c => c.ArchivedAt == null)).ThenInclude(c => c.Ingredient);
}
