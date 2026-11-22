using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Models;

namespace RestaurantHandler.Tests.Policies;

public class AvailabilityPolicyTests
{
    private readonly Category _burgers = new() { Id = Guid.NewGuid(), Name = "Burgers" };
    private readonly Ingredient _steak = new() { Id = Guid.NewGuid(), Name = "Steak" };
    private readonly Ingredient _tomate = new() { Id = Guid.NewGuid(), Name = "Tomate" };
    private readonly Ingredient _cheddar = new() { Id = Guid.NewGuid(), Name = "Cheddar" };
    private readonly Ingredient _salade = new() { Id = Guid.NewGuid(), Name = "Salade" };

    private Product Burger()
    {
        var p = new Product { Id = Guid.NewGuid(), Name = "Classic", Category = _burgers, CategoryId = _burgers.Id };
        p.Ingredients.Add(new ProductIngredient { ProductId = p.Id, IngredientId = _steak.Id, Ingredient = _steak, IsRemovable = false });
        var tomate = new ProductIngredient { ProductId = p.Id, IngredientId = _tomate.Id, Ingredient = _tomate, IsRemovable = true };
        tomate.Substitutions.Add(new IngredientSubstitution { SubstituteIngredientId = _salade.Id, SubstituteIngredient = _salade });
        p.Ingredients.Add(tomate);
        p.Supplements.Add(new ProductSupplement { IngredientId = _cheddar.Id, Ingredient = _cheddar, PriceCents = 50 });
        return p;
    }

    private static Menu MenuWith(Product plat, Product boisson, int minBoisson = 1)
    {
        var menu = new Menu { Name = "Menu" };
        var s1 = new MenuSlot { Label = "Plat", MinChoices = 1 };
        s1.Options.Add(new MenuSlotOption { Product = plat });
        var s2 = new MenuSlot { Label = "Boisson", MinChoices = minBoisson, MaxChoices = 1 };
        s2.Options.Add(new MenuSlotOption { Product = boisson });
        menu.Slots.Add(s1);
        menu.Slots.Add(s2);
        return menu;
    }

    private Product Drink() => new() { Name = "Cola", Category = _burgers, Kind = ProductKind.Drink };

    [Fact]
    public void Produit_complet_commandable_sans_retrait_impose()
    {
        var result = AvailabilityPolicy.ForProduct(Burger());

        Assert.True(result.IsOrderable);
        Assert.Empty(result.ForcedRemovalIngredientIds);
    }

    [Fact]
    public void Ingredient_non_retirable_en_rupture_rend_le_produit_indisponible()
    {
        _steak.IsAvailable = false;

        Assert.False(AvailabilityPolicy.ForProduct(Burger()).IsOrderable);
    }

    [Fact]
    public void Ingredient_retirable_en_rupture_impose_le_retrait()
    {
        _tomate.IsAvailable = false;

        var result = AvailabilityPolicy.ForProduct(Burger());

        Assert.True(result.IsOrderable);
        Assert.Equal([_tomate.Id], result.ForcedRemovalIngredientIds);
    }

    [Fact]
    public void Ingredient_archive_compte_comme_rupture()
    {
        _steak.ArchivedAt = DateTimeOffset.UtcNow;

        Assert.False(AvailabilityPolicy.ForProduct(Burger()).IsOrderable);
    }

    [Fact]
    public void Supplement_et_substitution_en_rupture_ne_sont_plus_proposes()
    {
        var burger = Burger();
        _cheddar.IsAvailable = false;
        _salade.IsAvailable = false;

        Assert.False(AvailabilityPolicy.IsSupplementOffered(burger.Supplements.Single()));
        Assert.False(AvailabilityPolicy.IsSubstitutionOffered(
            burger.Ingredients.Single(i => i.IngredientId == _tomate.Id).Substitutions.Single()));
    }

    [Theory]
    [InlineData("rupture")]
    [InlineData("inactif")]
    [InlineData("archive")]
    [InlineData("categorie-inactive")]
    [InlineData("categorie-archivee")]
    public void Produit_marque_ou_masque_indisponible(string cas)
    {
        var burger = Burger();
        switch (cas)
        {
            case "rupture": burger.IsAvailable = false; break;
            case "inactif": burger.IsActive = false; break;
            case "archive": burger.ArchivedAt = DateTimeOffset.UtcNow; break;
            case "categorie-inactive": _burgers.IsActive = false; break;
            case "categorie-archivee": _burgers.ArchivedAt = DateTimeOffset.UtcNow; break;
        }

        Assert.False(AvailabilityPolicy.ForProduct(burger).IsOrderable);
    }

    [Fact]
    public void Menu_indisponible_si_un_emplacement_obligatoire_n_a_plus_d_option()
    {
        _steak.IsAvailable = false;

        Assert.False(AvailabilityPolicy.IsMenuOrderable(MenuWith(Burger(), Drink())));
    }

    [Fact]
    public void Menu_reste_commandable_si_l_emplacement_vide_est_facultatif()
    {
        var drink = Drink();
        drink.IsAvailable = false;

        Assert.True(AvailabilityPolicy.IsMenuOrderable(MenuWith(Burger(), drink, minBoisson: 0)));
        Assert.False(AvailabilityPolicy.IsMenuOrderable(MenuWith(Burger(), drink, minBoisson: 1)));
    }

    [Fact]
    public void Menu_commandable_si_une_option_reste_disponible()
    {
        var menu = MenuWith(Burger(), Drink());
        var autre = Drink();
        autre.IsAvailable = false;
        menu.Slots.Last().Options.Add(new MenuSlotOption { Product = autre });

        Assert.True(AvailabilityPolicy.IsMenuOrderable(menu));
    }

    [Fact]
    public void Menu_inactif_indisponible()
    {
        var menu = MenuWith(Burger(), Drink());
        menu.IsActive = false;

        Assert.False(AvailabilityPolicy.IsMenuOrderable(menu));
    }
}
