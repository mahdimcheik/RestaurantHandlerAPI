using Microsoft.EntityFrameworkCore;
using RestaurantHandler.Contexts;
using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Models;

namespace RestaurantHandler.Services;

/// <summary>Charge la carte en vigueur et applique <see cref="CartPolicy"/> (jamais depuis le cache).</summary>
public class CartService(MainContext db)
{
    public async Task<PricedCart> PriceAsync(IReadOnlyList<CartLineDto> lines)
    {
        var cartLines = lines.Select(l => l.ToCartLine()).ToList();
        var productIds = cartLines.Where(l => l.ProductId is not null).Select(l => l.ProductId!.Value)
            .Concat(cartLines.SelectMany(l => l.Choices).Select(c => c.ProductId))
            .ToHashSet();
        var menuIds = cartLines.Where(l => l.MenuId is not null).Select(l => l.MenuId!.Value).ToHashSet();

        // Requêtes suivies dans le même contexte : les options de menu se relient aux produits chargés.
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .Include(p => p.Category)
            .Include(p => p.Ingredients).ThenInclude(pi => pi.Ingredient)
            .Include(p => p.OptionGroups).ThenInclude(l => l.OptionGroup)
            .ThenInclude(g => g!.Choices.Where(c => c.ArchivedAt == null)).ThenInclude(c => c.Ingredient)
            .AsSplitQuery()
            .ToDictionaryAsync(p => p.Id);
        var menus = await db.Menus
            .Where(m => menuIds.Contains(m.Id))
            .Include(m => m.Slots.Where(s => s.ArchivedAt == null))
            .ThenInclude(s => s.Options)
            .ToDictionaryAsync(m => m.Id);

        var substituteIds = cartLines
            .SelectMany(l => l.Modifications.Concat(l.Choices.SelectMany(c => c.Modifications)))
            .Where(m => m.Type == ModificationType.Replace && m.SubstituteIngredientId is not null)
            .Select(m => m.SubstituteIngredientId!.Value)
            .ToHashSet();
        var replacements = await db.Ingredients
            .Where(i => substituteIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id);

        return CartPolicy.Price(cartLines, products, menus, replacements);
    }

    /// <summary>Prix et contrôles sans rien créer : le panier peut s'afficher à jour avant validation.</summary>
    public async Task<QuoteResult> QuoteAsync(QuoteRequest dto)
    {
        var cart = await PriceAsync(dto.Lines);
        if (!cart.IsValid)
            throw new CartRejectedException(cart.Issues);

        return new QuoteResult
        {
            TotalCents = cart.TotalCents,
            Lines = cart.Items
                .Select((item, index) => new QuoteLine
                {
                    Index = index,
                    Name = item.NameSnapshot,
                    Quantity = item.Quantity,
                    UnitTotalCents = OrderPricing.UnitTotal(item),
                    LineTotalCents = OrderPricing.LineTotal(item),
                })
                .ToList(),
            Vat = OrderMapping.Vat(cart.Items),
        };
    }
}
