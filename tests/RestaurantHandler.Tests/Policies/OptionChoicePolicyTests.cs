using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Models;

namespace RestaurantHandler.Tests.Policies;

/// <summary>Groupes d'options (R4) : pain obligatoire, sauces bornées avec gratuité, taille avec surcoût.</summary>
public class OptionChoicePolicyTests
{
    private readonly CatalogFixture _c = new();

    private PricedCart Price(params CartLine[] lines) => CartPolicy.Price(lines, _c.Products, _c.Menus);

    private static CartLine Product(Guid id, params OptionChoice[] choices) =>
        new(id, null, 1, [], [], choices.Select(c => c.Id).ToList());

    private OptionChoice Pain(string name) => _c.ChoiceOf(_c.PainGroup, name);
    private OptionChoice Sauce(string name) => _c.ChoiceOf(_c.SauceGroup, name);
    private OptionChoice Taille(string name) => _c.ChoiceOf(_c.TailleGroup, name);

    [Fact]
    public void Pain_et_deux_sauces_offertes_au_prix_du_produit_avec_libelles()
    {
        var cart = Price(Product(_c.Kebab.Id, Sauce("Algérienne"), Pain("Pita"), Sauce("Blanche")));

        Assert.True(cart.IsValid);
        Assert.Equal(800, cart.TotalCents);
        var mods = cart.Items.Single().Modifications;
        Assert.All(mods, m => Assert.Equal(ModificationType.Choice, m.Type));
        Assert.Equal(["Pain : Pita", "Sauces : Blanche", "Sauces : Algérienne"], mods.Select(m => m.LabelSnapshot));
        Assert.Equal(_c.Pita.Id, mods.First().IngredientId);
    }

    [Fact]
    public void Troisieme_sauce_payante_quel_que_soit_l_ordre_de_la_requete()
    {
        var first = Price(Product(_c.Kebab.Id, Pain("Baguette"), Sauce("Samouraï"), Sauce("Blanche"), Sauce("Algérienne")));
        var second = Price(Product(_c.Kebab.Id, Sauce("Blanche"), Sauce("Algérienne"), Pain("Baguette"), Sauce("Samouraï")));

        Assert.Equal(850, first.TotalCents);
        Assert.Equal(850, second.TotalCents);
        // Les sauces au-delà des offertes sont facturées dans l'ordre de la carte.
        Assert.Equal(50, first.Items[0].Modifications.Single(m => m.LabelSnapshot == "Sauces : Samouraï").UnitPriceCents);
    }

    [Fact]
    public void Sans_sauce_accepte_quand_le_minimum_est_zero()
    {
        var cart = Price(Product(_c.Kebab.Id, Pain("Baguette")));

        Assert.True(cart.IsValid);
        Assert.Single(cart.Items[0].Modifications);
    }

    [Fact]
    public void Sans_pain_refuse()
    {
        var cart = Price(Product(_c.Kebab.Id, Sauce("Blanche")));

        Assert.False(cart.IsValid);
        Assert.Contains(cart.Issues, i => i.Message == "Kebab : choisissez 1 « Pain ».");
    }

    [Fact]
    public void Deux_pains_refuse()
    {
        var cart = Price(Product(_c.Kebab.Id, Pain("Baguette"), Pain("Pita")));

        Assert.False(cart.IsValid);
    }

    [Fact]
    public void Au_dela_du_maximum_de_sauces_refuse()
    {
        _c.SauceGroup.MaxChoices = 2;

        var cart = Price(Product(_c.Kebab.Id, Pain("Baguette"), Sauce("Blanche"), Sauce("Algérienne"), Sauce("Samouraï")));

        Assert.False(cart.IsValid);
        Assert.Contains(cart.Issues, i => i.Message == "Kebab : au plus 2 « Sauces ».");
    }

    [Fact]
    public void Choix_en_double_refuse()
    {
        var cart = Price(Product(_c.Kebab.Id, Pain("Baguette"), Sauce("Blanche"), Sauce("Blanche")));

        Assert.False(cart.IsValid);
        Assert.Contains(cart.Issues, i => i.Message.Contains("ne figure qu'une fois"));
    }

    [Fact]
    public void Choix_d_un_autre_produit_refuse()
    {
        var cart = Price(Product(_c.Kebab.Id, Pain("Baguette"), Taille("33 cm")));

        Assert.False(cart.IsValid);
        Assert.Contains(cart.Issues, i => i.Message == "Kebab : ce choix n'est pas proposé.");
    }

    [Fact]
    public void Choix_archive_refuse()
    {
        Sauce("Samouraï").ArchivedAt = DateTimeOffset.UtcNow;

        var cart = Price(Product(_c.Kebab.Id, Pain("Baguette"), Sauce("Samouraï")));

        Assert.False(cart.IsValid);
    }

    [Fact]
    public void Sauce_en_rupture_refusee_mais_le_produit_reste_commandable()
    {
        _c.Samourai.IsAvailable = false;

        var refused = Price(Product(_c.Kebab.Id, Pain("Baguette"), Sauce("Samouraï")));
        var accepted = Price(Product(_c.Kebab.Id, Pain("Baguette"), Sauce("Blanche")));

        Assert.Contains(refused.Issues, i => i.Message == "Kebab : Samouraï est en rupture.");
        Assert.True(accepted.IsValid);
        Assert.True(AvailabilityPolicy.ForProduct(_c.Kebab).IsOrderable);
    }

    [Fact]
    public void Plus_aucun_pain_disponible_rend_le_produit_indisponible()
    {
        _c.Baguette.IsAvailable = false;
        Assert.True(AvailabilityPolicy.ForProduct(_c.Kebab).IsOrderable);

        _c.Pita.IsAvailable = false;
        Assert.False(AvailabilityPolicy.ForProduct(_c.Kebab).IsOrderable);
    }

    [Fact]
    public void Groupe_archive_ignore()
    {
        _c.PainGroup.ArchivedAt = DateTimeOffset.UtcNow;

        var cart = Price(Product(_c.Kebab.Id));

        Assert.True(cart.IsValid);
        Assert.Equal(800, cart.TotalCents);
    }

    [Fact]
    public void Grande_taille_ajoute_son_surcout()
    {
        var cart = Price(new CartLine(_c.Margherita.Id, null, 2, [], [], [Taille("33 cm").Id]));

        Assert.Equal((1000 + 300) * 2, cart.TotalCents);
        var mod = cart.Items[0].Modifications.Single();
        Assert.Equal("Taille : 33 cm", mod.LabelSnapshot);
        Assert.Null(mod.IngredientId);
    }

    [Fact]
    public void Choix_d_option_par_la_liste_des_modifications_refuse()
    {
        var cart = Price(new CartLine(_c.Kebab.Id, null, 1,
            [new CartModification(ModificationType.Choice, _c.Baguette.Id)], [], [Pain("Baguette").Id]));

        Assert.False(cart.IsValid);
    }

    [Fact]
    public void Composant_de_menu_porte_ses_propres_choix()
    {
        _c.PlatSlot.Options.Add(new MenuSlotOption { ProductId = _c.Kebab.Id, Product = _c.Kebab });

        var ok = Price(new CartLine(null, _c.Menu.Id, 1, [], [
            new CartMenuChoice(_c.PlatSlot.Id, _c.Kebab.Id, [], [Pain("Pita").Id, Sauce("Blanche").Id, Sauce("Algérienne").Id, Sauce("Samouraï").Id]),
            new CartMenuChoice(_c.BoissonSlot.Id, _c.Cola.Id, []),
        ]));
        var missingBread = Price(new CartLine(null, _c.Menu.Id, 1, [], [
            new CartMenuChoice(_c.PlatSlot.Id, _c.Kebab.Id, []),
            new CartMenuChoice(_c.BoissonSlot.Id, _c.Cola.Id, []),
        ]));

        // Prix du menu + 3e sauce
        Assert.Equal(1290 + 50, ok.TotalCents);
        Assert.Contains(missingBread.Issues, i => i.Message == "Menu Classic : Kebab : choisissez 1 « Pain ».");
    }

    [Fact]
    public void Choix_d_option_sur_la_ligne_menu_refuse()
    {
        var cart = Price(new CartLine(null, _c.Menu.Id, 1, [], [], [Pain("Pita").Id]));

        Assert.Contains(cart.Issues, i => i.Message == "Un menu se personnalise produit par produit.");
    }
}
