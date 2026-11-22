using RestaurantHandler.Models;

namespace RestaurantHandler.Tests.Policies;

/// <summary>
/// Petite carte en mémoire : Classic (steak fixe, tomate retirable → salade +0,30, cheddar en
/// supplément), Cola, menu ; Kebab (pain 1 parmi 2, sauces 0 à 3 dont 2 offertes puis +0,50),
/// Margherita (taille 29 cm ou 33 cm +3 €).
/// </summary>
public class CatalogFixture
{
    public readonly Category Burgers = new() { Id = Guid.NewGuid(), Name = "Burgers" };
    public readonly Ingredient Steak = new() { Id = Guid.NewGuid(), Name = "Steak" };
    public readonly Ingredient Tomate = new() { Id = Guid.NewGuid(), Name = "Tomate" };
    public readonly Ingredient Salade = new() { Id = Guid.NewGuid(), Name = "Salade" };
    public readonly Ingredient Cheddar = new() { Id = Guid.NewGuid(), Name = "Cheddar" };
    public readonly Ingredient Baguette = new() { Id = Guid.NewGuid(), Name = "Baguette" };
    public readonly Ingredient Pita = new() { Id = Guid.NewGuid(), Name = "Pita" };
    public readonly Ingredient Blanche = new() { Id = Guid.NewGuid(), Name = "Blanche" };
    public readonly Ingredient Algerienne = new() { Id = Guid.NewGuid(), Name = "Algérienne" };
    public readonly Ingredient Samourai = new() { Id = Guid.NewGuid(), Name = "Samouraï" };
    public readonly Product Classic;
    public readonly Product Kebab;
    public readonly OptionGroup PainGroup;
    public readonly OptionGroup SauceGroup;
    public readonly Product Margherita;
    public readonly OptionGroup TailleGroup;
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

        Kebab = new Product { Id = Guid.NewGuid(), Name = "Kebab", Category = Burgers, PriceCents = 800, VatRate = 10 };
        PainGroup = Group("Pain", 1, 1, 1, 0, Choice("Baguette", 0, Baguette), Choice("Pita", 0, Pita));
        SauceGroup = Group("Sauces", 0, 3, 2, 50,
            Choice("Blanche", 0, Blanche), Choice("Algérienne", 0, Algerienne), Choice("Samouraï", 0, Samourai));
        Link(Kebab, PainGroup, 0);
        Link(Kebab, SauceGroup, 1);

        Margherita = new Product { Id = Guid.NewGuid(), Name = "Margherita", Category = Burgers, PriceCents = 1000, VatRate = 10 };
        TailleGroup = Group("Taille", 1, 1, 1, 0, Choice("29 cm", 0), Choice("33 cm", 300));
        Link(Margherita, TailleGroup, 0);

        Menu = new Menu { Id = Guid.NewGuid(), Name = "Menu Classic", PriceCents = 1290, VatRate = 10 };
        PlatSlot = new MenuSlot { Id = Guid.NewGuid(), Label = "Plat", Position = 0, MinChoices = 1, MaxChoices = 1 };
        PlatSlot.Options.Add(new MenuSlotOption { ProductId = Classic.Id, Product = Classic });
        BoissonSlot = new MenuSlot { Id = Guid.NewGuid(), Label = "Boisson", Position = 1, MinChoices = 1, MaxChoices = 1 };
        BoissonSlot.Options.Add(new MenuSlotOption { ProductId = Cola.Id, Product = Cola });
        BoissonSlot.Options.Add(new MenuSlotOption { ProductId = GrandCola.Id, Product = GrandCola, ExtraPriceCents = 100 });
        Menu.Slots.Add(PlatSlot);
        Menu.Slots.Add(BoissonSlot);
    }

    public Dictionary<Guid, Product> Products =>
        new[] { Classic, Cola, GrandCola, Kebab, Margherita }.ToDictionary(p => p.Id);

    public OptionChoice ChoiceOf(OptionGroup group, string name) => group.Choices.Single(c => c.Name == name);

    private static void Link(Product product, OptionGroup group, int position) =>
        product.OptionGroups.Add(new ProductOptionGroup
        {
            ProductId = product.Id, Product = product, OptionGroupId = group.Id, OptionGroup = group, Position = position,
        });

    private static OptionGroup Group(string label, int min, int max, int free, int extra, params OptionChoice[] choices)
    {
        var group = new OptionGroup
        {
            Id = Guid.NewGuid(), Label = label,
            MinChoices = min, MaxChoices = max, FreeChoices = free, ExtraChoicePriceCents = extra,
        };
        for (var i = 0; i < choices.Length; i++)
        {
            choices[i].Position = i;
            choices[i].GroupId = group.Id;
            group.Choices.Add(choices[i]);
        }
        return group;
    }

    private static OptionChoice Choice(string name, int extra, Ingredient? ingredient = null) =>
        new() { Id = Guid.NewGuid(), Name = name, ExtraPriceCents = extra, IngredientId = ingredient?.Id, Ingredient = ingredient };
    public Dictionary<Guid, Menu> Menus => new[] { Menu }.ToDictionary(m => m.Id);
}
