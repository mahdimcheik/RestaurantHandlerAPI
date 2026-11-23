using Microsoft.EntityFrameworkCore;
using RestaurantHandler.Contexts;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Models;

namespace RestaurantHandler.Services;

/// <summary>
/// Menus, emplacements et options (US-E02-6). Menus et emplacements sont archivés, jamais
/// supprimés : les lignes de commande y pointent. Les options (liaisons) sont remplaçables.
/// </summary>
public class MenuService(MainContext db, CatalogImageService images, CatalogChangeNotifier changes)
{
    public async Task<List<MenuDetails>> ListAsync(bool includeArchived)
    {
        var menus = await WithSlots(db.Menus.AsNoTracking())
            .Where(m => includeArchived || m.ArchivedAt == null)
            .OrderBy(m => m.ArchivedAt != null).ThenBy(m => m.Position).ThenBy(m => m.Name)
            .ToListAsync();

        return menus.Select(ToDetails).ToList();
    }

    public async Task<MenuDetails> GetAsync(Guid id) =>
        ToDetails(
            await WithSlots(db.Menus.AsNoTracking()).FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new ResourceNotFoundException("Menu introuvable.")
        );

    public async Task<MenuDetails> CreateAsync(MenuWrite dto)
    {
        var position = await db.Menus.Where(m => m.ArchivedAt == null).MaxAsync(m => (int?)m.Position) ?? -1;
        var menu = new Menu { Position = position + 1 };
        Apply(menu, dto);
        db.Menus.Add(menu);
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();

        return await GetAsync(menu.Id);
    }

    public async Task<MenuDetails> UpdateAsync(Guid id, MenuWrite dto)
    {
        var menu = await FindAsync(id);
        Apply(menu, dto);
        menu.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();

        return await GetAsync(id);
    }

    public async Task<List<MenuDetails>> ReorderAsync(ReorderRequest dto)
    {
        var menus = await db.Menus.Where(m => m.ArchivedAt == null).ToListAsync();
        CatalogOrdering.Apply(menus, m => m.Id, dto.OrderedIds, (m, position) => m.Position = position);
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();

        return await ListAsync(false);
    }

    public async Task ArchiveAsync(Guid id)
    {
        var menu = await FindAsync(id);
        menu.ArchivedAt = DateTimeOffset.UtcNow;
        menu.IsActive = false;
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();
    }

    public async Task<MenuDetails> SetImageAsync(Guid id, IFormFile file, CancellationToken ct)
    {
        var menu = await FindAsync(id);
        var key = await images.StoreAsync(file, ct);
        var previous = menu.ImageUrl;

        menu.ImageUrl = key;
        menu.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await images.DeleteAsync(previous);
        await changes.CatalogChangedAsync();

        return await GetAsync(id);
    }

    public async Task<MenuDetails> RemoveImageAsync(Guid id, CancellationToken ct)
    {
        var menu = await FindAsync(id);
        var previous = menu.ImageUrl;
        if (previous is not null)
        {
            menu.ImageUrl = null;
            menu.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            await images.DeleteAsync(previous);
            await changes.CatalogChangedAsync();
        }

        return await GetAsync(id);
    }

    // ---------- Emplacements ----------

    public async Task<MenuDetails> AddSlotAsync(Guid menuId, MenuSlotWrite dto)
    {
        await FindAsync(menuId);
        var position = await db.MenuSlots.Where(s => s.MenuId == menuId && s.ArchivedAt == null).MaxAsync(s => (int?)s.Position) ?? -1;

        db.MenuSlots.Add(new MenuSlot
        {
            MenuId = menuId,
            Label = dto.Label.Trim(),
            MinChoices = dto.MinChoices,
            MaxChoices = dto.MaxChoices,
            Position = position + 1,
        });
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();

        return await GetAsync(menuId);
    }

    public async Task<MenuDetails> UpdateSlotAsync(Guid menuId, Guid slotId, MenuSlotWrite dto)
    {
        var slot = await FindSlotAsync(menuId, slotId);
        slot.Label = dto.Label.Trim();
        slot.MinChoices = dto.MinChoices;
        slot.MaxChoices = dto.MaxChoices;
        slot.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();

        return await GetAsync(menuId);
    }

    public async Task<MenuDetails> ArchiveSlotAsync(Guid menuId, Guid slotId)
    {
        var slot = await FindSlotAsync(menuId, slotId);
        slot.ArchivedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();

        return await GetAsync(menuId);
    }

    /// <summary>
    /// La position est unique par menu (index en base, non différable) : on passe d'abord par
    /// des positions négatives pour qu'aucun échange intermédiaire ne viole l'index.
    /// </summary>
    public async Task<MenuDetails> ReorderSlotsAsync(Guid menuId, ReorderRequest dto)
    {
        await FindAsync(menuId);
        var slots = await db.MenuSlots.Where(s => s.MenuId == menuId && s.ArchivedAt == null).ToListAsync();
        CatalogOrdering.Apply(slots, s => s.Id, dto.OrderedIds, (_, _) => { });

        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (var slot in slots)
            slot.Position = -1 - slot.Position;
        await db.SaveChangesAsync();
        CatalogOrdering.Apply(slots, s => s.Id, dto.OrderedIds, (s, position) => s.Position = position);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        await changes.CatalogChangedAsync();

        return await GetAsync(menuId);
    }

    /// <summary>Remplace les options d'un emplacement ; chaque produit doit être actif et non archivé.</summary>
    public async Task<MenuDetails> SetSlotOptionsAsync(Guid menuId, Guid slotId, SlotOptionsUpdate dto)
    {
        var slot = await db.MenuSlots.Include(s => s.Options)
            .FirstOrDefaultAsync(s => s.Id == slotId && s.MenuId == menuId && s.ArchivedAt == null)
            ?? throw new ResourceNotFoundException("Emplacement introuvable.");

        var productIds = dto.Options.Select(o => o.ProductId).ToList();
        var valid = await db.Products.CountAsync(p => productIds.Contains(p.Id) && p.ArchivedAt == null && p.IsActive);
        if (valid != productIds.Count)
            throw new BusinessRuleException("Une option référence un produit introuvable, inactif ou archivé.");

        var wanted = dto.Options.ToDictionary(o => o.ProductId);
        foreach (var removed in slot.Options.Where(o => !wanted.ContainsKey(o.ProductId)).ToList())
            db.MenuSlotOptions.Remove(removed);
        foreach (var item in dto.Options)
        {
            var option = slot.Options.FirstOrDefault(o => o.ProductId == item.ProductId);
            if (option is null)
                slot.Options.Add(new MenuSlotOption { SlotId = slotId, ProductId = item.ProductId, ExtraPriceCents = item.ExtraPriceCents });
            else
                option.ExtraPriceCents = item.ExtraPriceCents;
        }

        slot.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await changes.CatalogChangedAsync();

        return await GetAsync(menuId);
    }

    private static IQueryable<Menu> WithSlots(IQueryable<Menu> query) =>
        query
            .Include(m => m.Slots.Where(s => s.ArchivedAt == null).OrderBy(s => s.Position))
            .ThenInclude(s => s.Options).ThenInclude(o => o.Product)
            .AsSplitQuery();

    private async Task<Menu> FindAsync(Guid id) =>
        await db.Menus.FirstOrDefaultAsync(m => m.Id == id && m.ArchivedAt == null)
        ?? throw new ResourceNotFoundException("Menu introuvable.");

    private async Task<MenuSlot> FindSlotAsync(Guid menuId, Guid slotId) =>
        await db.MenuSlots.FirstOrDefaultAsync(s => s.Id == slotId && s.MenuId == menuId && s.ArchivedAt == null)
        ?? throw new ResourceNotFoundException("Emplacement introuvable.");

    private static void Apply(Menu menu, MenuWrite dto)
    {
        menu.Name = dto.Name.Trim();
        menu.Description = dto.Description?.Trim();
        menu.PriceCents = dto.PriceCents;
        menu.VatRate = dto.VatRate;
        menu.IsActive = dto.IsActive;
    }

    private MenuDetails ToDetails(Menu m) =>
        new()
        {
            Id = m.Id,
            Name = m.Name,
            Description = m.Description,
            ImageUrl = images.Url(m.ImageUrl),
            PriceCents = m.PriceCents,
            VatRate = m.VatRate,
            Position = m.Position,
            IsActive = m.IsActive,
            IsArchived = m.ArchivedAt != null,
            Slots = m.Slots
                .Select(s => new MenuSlotDetails
                {
                    Id = s.Id,
                    Label = s.Label,
                    Position = s.Position,
                    MinChoices = s.MinChoices,
                    MaxChoices = s.MaxChoices,
                    Options = s.Options
                        .OrderBy(o => o.Product?.Name)
                        .Select(o => new SlotOptionDetails
                        {
                            ProductId = o.ProductId,
                            ProductName = o.Product?.Name ?? string.Empty,
                            ExtraPriceCents = o.ExtraPriceCents,
                        })
                        .ToList(),
                })
                .ToList(),
        };
}
