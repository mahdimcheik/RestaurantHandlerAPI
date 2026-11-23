using RestaurantHandler.Models;

namespace RestaurantHandler.Tests.Policies;

/// <summary>
/// Petite carte en mémoire : Classic (steak fixe, tomate retirable et remplaçable ; prix de
/// référence : tomate et salade 0,30, saumon 3,30, cheddar 0,80 ; pâte non proposée en
/// remplacement), Cola, menu ; Kebab (pain 1 parmi 2, sauces 0 à 3 dont 2 offertes puis +0,50),
/// Margherita (taille 29 cm ou 33 cm +3 €) ; Pizza 3 et Pizza 5 partageant « Ingrédients »
/// (chacun au plus 2 fois, +1,50 au-delà des inclus) : 3 exactement, ou 5 à 6 dont 5 inclus.
/// </summary>
public class CatalogFixture
{
    public readonly Category Burgers = new() { Id = Guid.NewGuid(), Name = "Burgers" };
    public readonly Ingredient Steak = new() { Id = Guid.NewGuid(), Name = "Steak" };
    public readonly Ingredient Tomate = new() { Id = Guid.NewGuid(), Name = "Tomate", PriceCents = 30 };
    public readonly Ingredient Salade = new() { Id = Guid.NewGuid(), Name = "Salade", PriceCents = 30 };
    public readonly Ingredient Cheddar = new() { Id = Guid.NewGuid(), Name = "Cheddar", PriceCents = 80 };
    public readonly Ingredient Saumon = new() { Id = Guid.NewGuid(), Name = "Saumon", PriceCents = 330 };
    public readonly Ingredient Pate = new() { Id = Guid.NewGuid(), Name = "Pâte", PriceCents = 100, IsOfferedAsReplacement = false };
    public readonly Ingredient Baguette = new() { Id = Guid.NewGuid(), Name = "Baguette" };
    public readonly Ingredient Pita = new() { Id = Guid.NewGuid(), Name = "Pita" };
    public readonly Ingredient Blanche = new() { Id = Guid.NewGuid(), Name = "Blanche" };
    public readonly Ingredient Algerienne = new() { Id = Guid.NewGuid(), Name = "Algérienne" };
    public readonly Ingredient Samourai = new() { Id = Guid.NewGuid(), Name = "Samouraï" };
    public readonly Ingredient Jambon = new() { Id = Guid.NewGuid(), Name = "Jambon" };
    public readonly Ingredient Oeuf = new() { Id = Guid.NewGuid(), Name = "Œuf" };
    public readonly Ingredient Olives = new() { Id = Guid.NewGuid(), Name = "Olives" };
    public readonly Product Classic;
    public readonly Product Kebab;
    public readonly OptionGroup PainGroup;
    public readonly OptionGroup SauceGroup;
    public readonly Product Margherita;
    public readonly OptionGroup TailleGroup;
    public readonly Product Pizza3;
    public readonly Product Pizza5;
    public readonly OptionGroup IngredientsGroup;
    public readonly Product Cola;
    public readonly Product GrandCola;
    public readonly Menu Menu;
    public readonly MenuSlot PlatSlot;
    public readonly MenuSlot BoissonSlot;

    public CatalogFixture()
    {
        Classic = new Product { Id = Guid.NewGuid(), Name = "Classic", Category = Burgers, PriceCents = 950, VatRate = 10 };
        Classic.Ingredients.Add(new ProductIngredient { IngredientId = Steak.Id, Ingredient = Steak, IsRemovable = false });
        Classic.Ingredients.Add(new ProductIngredient { IngredientId = Tomate.Id, Ingredient = Tomate, IsRemovable = true });

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

        IngredientsGroup = Group("Ingrédients", 3, 3, 3, 150,
            Choice("Jambon", 0, Jambon), Choice("Œuf", 0, Oeuf), Choice("Olives", 0, Olives));
        IngredientsGroup.MaxPerChoice = 2;
        Pizza3 = new Product { Id = Guid.NewGuid(), Name = "Pizza 3", Category = Burgers, PriceCents = 900, VatRate = 10 };
        Link(Pizza3, IngredientsGroup, 0);
        Pizza5 = new Product { Id = Guid.NewGuid(), Name = "Pizza 5", Category = Burgers, PriceCents = 1100, VatRate = 10 };
        Link(Pizza5, IngredientsGroup, 0);
        var rules = Pizza5.OptionGroups.Single();
        (rules.MinChoices, rules.MaxChoices, rules.FreeChoices) = (5, 6, 5);

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
        new[] { Classic, Cola, GrandCola, Kebab, Margherita, Pizza3, Pizza5 }.ToDictionary(p => p.Id);

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

    /// <summary>Tous les ingrédients : le service charge ceux demandés en remplacement.</summary>
    public Dictionary<Guid, Ingredient> Replacements =>
        new[] { Steak, Tomate, Salade, Cheddar, Saumon, Pate, Baguette, Pita, Blanche, Algerienne, Samourai, Jambon, Oeuf, Olives }
            .ToDictionary(i => i.Id);
}
