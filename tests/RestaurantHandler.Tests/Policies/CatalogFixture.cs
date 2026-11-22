using RestaurantHandler.Models;

namespace RestaurantHandler.Tests.Policies;

/// <summary>Petite carte en mémoire : Classic (steak fixe, tomate retirable → salade +0,30, cheddar en supplément), Cola, menu.</summary>
public class CatalogFixture
{
    public readonly Category Burgers = new() { Id = Guid.NewGuid(), Name = "Burgers" };
    public readonly Ingredient Steak = new() { Id = Guid.NewGuid(), Name = "Steak" };
    public readonly Ingredient Tomate = new() { Id = Guid.NewGuid(), Name = "Tomate" };
    public readonly Ingredient Salade = new() { Id = Guid.NewGuid(), Name = "Salade" };
    public readonly Ingredient Cheddar = new() { Id = Guid.NewGuid(), Name = "Cheddar" };
    public readonly Product Classic;
    public readonly Product Cola;
    public readonly Product GrandCola;
    public readonly Menu Menu;
    public readonly MenuSlot PlatSlot;
    public readonly MenuSlot BoissonSlot;

    public CatalogFixture()
    {
        Classic = new Product { Id = Guid.NewGuid(), Name = "Classic", Category = Burgers, PriceCents = 950, VatRate = 10 };
        Classic.Ingredients.Add(new ProductIngredient { IngredientId = Steak.Id, Ingredient = Steak, IsRemovable = false });
        var tomate = new ProductIngredient { IngredientId = Tomate.Id, Ingredient = Tomate, IsRemovable = true };
        tomate.Substitutions.Add(new IngredientSubstitution
        {
            IngredientId = Tomate.Id, SubstituteIngredientId = Salade.Id, SubstituteIngredient = Salade, ExtraPriceCents = 30,
        });
        Classic.Ingredients.Add(tomate);
        Classic.Supplements.Add(new ProductSupplement { IngredientId = Cheddar.Id, Ingredient = Cheddar, PriceCents = 80, MaxQuantity = 3 });

        Cola = new Product { Id = Guid.NewGuid(), Name = "Cola", Category = Burgers, PriceCents = 250, VatRate = 5.5m };
        GrandCola = new Product { Id = Guid.NewGuid(), Name = "Grand Cola", Category = Burgers, PriceCents = 350, VatRate = 5.5m };

        Menu = new Menu { Id = Guid.NewGuid(), Name = "Menu Classic", PriceCents = 1290, VatRate = 10 };
        PlatSlot = new MenuSlot { Id = Guid.NewGuid(), Label = "Plat", Position = 0, MinChoices = 1, MaxChoices = 1 };
        PlatSlot.Options.Add(new MenuSlotOption { ProductId = Classic.Id, Product = Classic });
        BoissonSlot = new MenuSlot { Id = Guid.NewGuid(), Label = "Boisson", Position = 1, MinChoices = 1, MaxChoices = 1 };
        BoissonSlot.Options.Add(new MenuSlotOption { ProductId = Cola.Id, Product = Cola });
        BoissonSlot.Options.Add(new MenuSlotOption { ProductId = GrandCola.Id, Product = GrandCola, ExtraPriceCents = 100 });
        Menu.Slots.Add(PlatSlot);
        Menu.Slots.Add(BoissonSlot);
    }

    public Dictionary<Guid, Product> Products => new[] { Classic, Cola, GrandCola }.ToDictionary(p => p.Id);
    public Dictionary<Guid, Menu> Menus => new[] { Menu }.ToDictionary(m => m.Id);
}
