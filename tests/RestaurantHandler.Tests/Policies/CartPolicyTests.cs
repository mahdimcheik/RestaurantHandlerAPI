using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Models;

namespace RestaurantHandler.Tests.Policies;

public class CartPolicyTests
{
    private readonly CatalogFixture _c = new();

    private PricedCart Price(params CartLine[] lines) => CartPolicy.Price(lines, _c.Products, _c.Menus, _c.Replacements);

    private static CartLine Product(Guid id, int qty = 1, params CartModification[] mods) => new(id, null, qty, mods, []);

    private CartLine MenuLine(int qty, params CartMenuChoice[] choices) => new(null, _c.Menu.Id, qty, [], choices);

    [Fact]
    public void Produit_simple_au_prix_de_la_carte_avec_snapshots()
    {
        var cart = Price(Product(_c.Classic.Id, 2));

        Assert.True(cart.IsValid);
        Assert.Equal(1900, cart.TotalCents);
        var item = cart.Items.Single();
        Assert.Equal("Classic", item.NameSnapshot);
        Assert.Equal(950, item.UnitPriceCents);
        Assert.Equal(10m, item.VatRate);
    }

    [Fact]
    public void Remplacement_au_meme_prix_gratuit()
    {
        var cart = Price(Product(_c.Classic.Id, 2, new CartModification(ModificationType.Replace, _c.Tomate.Id, _c.Salade.Id)));

        Assert.Equal(1900, cart.TotalCents);
        Assert.Equal("Tomate → Salade", cart.Items[0].Modifications.Single().LabelSnapshot);
    }

    [Fact]
    public void Remplacement_par_plus_cher_facture_la_difference()
    {
        var cart = Price(Product(_c.Classic.Id, 2, new CartModification(ModificationType.Replace, _c.Tomate.Id, _c.Saumon.Id)));

        // (950 + (330 − 30)) × 2
        Assert.Equal(2500, cart.TotalCents);
        Assert.Equal(300, cart.Items[0].Modifications.Single().UnitPriceCents);
    }

    [Fact]
    public void Remplacement_par_moins_cher_ne_baisse_pas_le_prix()
    {
        _c.Salade.PriceCents = 10;

        var cart = Price(Product(_c.Classic.Id, 1, new CartModification(ModificationType.Replace, _c.Tomate.Id, _c.Salade.Id)));

        Assert.Equal(950, cart.TotalCents);
    }

    [Fact]
    public void Retrait_ne_baisse_jamais_le_prix()
    {
        var cart = Price(Product(_c.Classic.Id, 1, new CartModification(ModificationType.Remove, _c.Tomate.Id)));

        Assert.Equal(950, cart.TotalCents);
        Assert.Equal("Sans Tomate", cart.Items[0].Modifications.Single().LabelSnapshot);
    }

    [Theory]
    [InlineData("retrait-non-retirable")]
    [InlineData("retrait-et-remplacement")]
    [InlineData("remplacement-non-retirable")]
    [InlineData("remplacement-par-lui-meme")]
    [InlineData("remplacement-inconnu")]
    [InlineData("remplacement-non-propose")]
    [InlineData("remplacement-archive")]
    [InlineData("supplement-hors-groupe")]
    public void Personnalisation_invalide_R4_refusee(string cas)
    {
        _c.Cheddar.ArchivedAt = cas == "remplacement-archive" ? DateTimeOffset.UtcNow : null;
        CartModification[] mods = cas switch
        {
            "retrait-non-retirable" => [new(ModificationType.Remove, _c.Steak.Id)],
            "retrait-et-remplacement" => [new(ModificationType.Remove, _c.Tomate.Id), new(ModificationType.Replace, _c.Tomate.Id, _c.Salade.Id)],
            "remplacement-non-retirable" => [new(ModificationType.Replace, _c.Steak.Id, _c.Saumon.Id)],
            "remplacement-par-lui-meme" => [new(ModificationType.Replace, _c.Tomate.Id, _c.Tomate.Id)],
            "remplacement-inconnu" => [new(ModificationType.Replace, _c.Tomate.Id, Guid.NewGuid())],
            "remplacement-non-propose" => [new(ModificationType.Replace, _c.Tomate.Id, _c.Pate.Id)],
            "remplacement-archive" => [new(ModificationType.Replace, _c.Tomate.Id, _c.Cheddar.Id)],
            _ => [new(ModificationType.Supplement, _c.Cheddar.Id)],
        };

        var cart = Price(Product(_c.Classic.Id, 1, mods));

        Assert.False(cart.IsValid);
        Assert.Empty(cart.Items);
        Assert.Equal(0, cart.Issues.Single().LineIndex);
    }

    [Fact]
    public void Rupture_non_retirable_survenue_apres_ajout_au_panier_refuse()
    {
        _c.Steak.IsAvailable = false;

        var cart = Price(Product(_c.Cola.Id), Product(_c.Classic.Id));

        Assert.False(cart.IsValid);
        Assert.Equal(1, cart.Issues.Single().LineIndex);
    }

    [Fact]
    public void Rupture_retirable_exige_le_retrait_ou_un_remplacement()
    {
        _c.Tomate.IsAvailable = false;

        Assert.False(Price(Product(_c.Classic.Id)).IsValid);
        Assert.True(Price(Product(_c.Classic.Id, 1, new CartModification(ModificationType.Remove, _c.Tomate.Id))).IsValid);
        Assert.True(Price(Product(_c.Classic.Id, 1, new CartModification(ModificationType.Replace, _c.Tomate.Id, _c.Salade.Id))).IsValid);
    }

    [Fact]
    public void Remplacant_en_rupture_refuse()
    {
        _c.Salade.IsAvailable = false;

        var cart = Price(Product(_c.Classic.Id, 1, new CartModification(ModificationType.Replace, _c.Tomate.Id, _c.Salade.Id)));

        Assert.Contains(cart.Issues, i => i.Message == "Classic : Salade est en rupture.");
    }

    [Fact]
    public void Menu_R5_surcout_option_et_personnalisation_du_composant()
    {
        var cart = Price(MenuLine(2,
            new CartMenuChoice(_c.PlatSlot.Id, _c.Classic.Id, [new(ModificationType.Replace, _c.Tomate.Id, _c.Cheddar.Id)]),
            new CartMenuChoice(_c.BoissonSlot.Id, _c.GrandCola.Id, [])));

        Assert.True(cart.IsValid);
        // (1290 + (80 − 30) + 100) × 2
        Assert.Equal(2880, cart.TotalCents);
        var menu = cart.Items.Single();
        Assert.Equal(2, menu.Children.Count);
        Assert.All(menu.Children, c => Assert.Equal(10m, c.VatRate));
        Assert.Equal(100, menu.Children.Single(c => c.ProductId == _c.GrandCola.Id).UnitPriceCents);
    }

    [Fact]
    public void Menu_emplacement_obligatoire_manquant_ou_trop_de_choix_refuse()
    {
        Assert.False(Price(MenuLine(1, new CartMenuChoice(_c.PlatSlot.Id, _c.Classic.Id, []))).IsValid);
        Assert.False(Price(MenuLine(1,
            new CartMenuChoice(_c.PlatSlot.Id, _c.Classic.Id, []),
            new CartMenuChoice(_c.BoissonSlot.Id, _c.Cola.Id, []),
            new CartMenuChoice(_c.BoissonSlot.Id, _c.GrandCola.Id, []))).IsValid);
    }

    [Fact]
    public void Menu_choix_hors_options_ou_indisponible_refuse()
    {
        Assert.False(Price(MenuLine(1,
            new CartMenuChoice(_c.PlatSlot.Id, _c.Cola.Id, []),
            new CartMenuChoice(_c.BoissonSlot.Id, _c.Cola.Id, []))).IsValid);

        _c.Cola.IsAvailable = false;
        Assert.False(Price(MenuLine(1,
            new CartMenuChoice(_c.PlatSlot.Id, _c.Classic.Id, []),
            new CartMenuChoice(_c.BoissonSlot.Id, _c.Cola.Id, []))).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Quantite_hors_bornes_refusee(int qty) => Assert.False(Price(Product(_c.Cola.Id, qty)).IsValid);

    [Fact]
    public void Panier_vide_ou_ligne_ambigue_refuse()
    {
        Assert.False(Price().IsValid);
        Assert.False(Price(new CartLine(_c.Cola.Id, _c.Menu.Id, 1, [], [])).IsValid);
    }

    [Fact]
    public void Snapshots_independants_de_la_carte_apres_validation()
    {
        var item = Price(Product(_c.Classic.Id)).Items.Single();

        _c.Classic.Name = "Classic v2";
        _c.Classic.PriceCents = 1200;

        Assert.Equal("Classic", item.NameSnapshot);
        Assert.Equal(950, OrderPricing.LineTotal(item));
    }
}
