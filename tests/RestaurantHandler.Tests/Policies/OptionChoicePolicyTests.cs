using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Models;

namespace RestaurantHandler.Tests.Policies;

/// <summary>Groupes d'options (R4) : pain obligatoire, sauces bornées, taille avec surcoût ; seul le surcoût des choix est facturé.</summary>
public class OptionChoicePolicyTests
{
    private readonly CatalogFixture _c = new();

    private PricedCart Price(params CartLine[] lines) => CartPolicy.Price(lines, _c.Products, _c.Menus, _c.Replacements);

    private static CartLine Product(Guid id, params OptionChoice[] choices) =>
        new(id, null, 1, [], [], choices.Select(c => new CartOptionChoice(c.Id)).ToList());

    private static CartLine Quantities(Guid id, params (OptionChoice Choice, int Quantity)[] choices) =>
        new(id, null, 1, [], [], choices.Select(c => new CartOptionChoice(c.Choice.Id, c.Quantity)).ToList());

    private OptionChoice Pain(string name) => _c.ChoiceOf(_c.PainGroup, name);
    private OptionChoice Sauce(string name) => _c.ChoiceOf(_c.SauceGroup, name);
    private OptionChoice Taille(string name) => _c.ChoiceOf(_c.TailleGroup, name);
    private OptionChoice Garniture(string name) => _c.ChoiceOf(_c.IngredientsGroup, name);

    [Fact]
    public void Pain_et_deux_sauces_sans_surcout_au_prix_du_produit_avec_libelles()
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
    public void Seul_le_surcout_du_choix_est_facture_quel_que_soit_l_ordre_de_la_requete()
    {
        Sauce("Samouraï").ExtraPriceCents = 100;

        var first = Price(Product(_c.Kebab.Id, Pain("Baguette"), Sauce("Samouraï"), Sauce("Blanche"), Sauce("Algérienne")));
        var second = Price(Product(_c.Kebab.Id, Sauce("Blanche"), Sauce("Algérienne"), Pain("Baguette"), Sauce("Samouraï")));

        Assert.Equal(900, first.TotalCents);
        Assert.Equal(900, second.TotalCents);
        var sauces = first.Items[0].Modifications.Where(m => m.LabelSnapshot.StartsWith("Sauces")).ToList();
        Assert.Equal([0, 0, 100], sauces.Select(m => m.UnitPriceCents));
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
        var cart = Price(new CartLine(_c.Margherita.Id, null, 2, [], [], [new CartOptionChoice(Taille("33 cm").Id)]));

        Assert.Equal((1000 + 300) * 2, cart.TotalCents);
        var mod = cart.Items[0].Modifications.Single();
        Assert.Equal("Taille : 33 cm", mod.LabelSnapshot);
        Assert.Null(mod.IngredientId);
    }

    [Fact]
    public void Choix_d_option_par_la_liste_des_modifications_refuse()
    {
        var cart = Price(new CartLine(_c.Kebab.Id, null, 1,
            [new CartModification(ModificationType.Choice, _c.Baguette.Id)], [], [new CartOptionChoice(Pain("Baguette").Id)]));

        Assert.False(cart.IsValid);
    }

    [Fact]
    public void Composant_de_menu_porte_ses_propres_choix()
    {
        _c.PlatSlot.Options.Add(new MenuSlotOption { ProductId = _c.Kebab.Id, Product = _c.Kebab });

        var ok = Price(new CartLine(null, _c.Menu.Id, 1, [], [
            new CartMenuChoice(_c.PlatSlot.Id, _c.Kebab.Id, [],
                new[] { Pain("Pita"), Sauce("Blanche"), Sauce("Algérienne"), Sauce("Samouraï") }.Select(c => new CartOptionChoice(c.Id)).ToList()),
            new CartMenuChoice(_c.BoissonSlot.Id, _c.Cola.Id, []),
        ]));
        var missingBread = Price(new CartLine(null, _c.Menu.Id, 1, [], [
            new CartMenuChoice(_c.PlatSlot.Id, _c.Kebab.Id, []),
            new CartMenuChoice(_c.BoissonSlot.Id, _c.Cola.Id, []),
        ]));

        Assert.Equal(1290, ok.TotalCents);
        Assert.Contains(missingBread.Issues, i => i.Message == "Menu Classic : Kebab : choisissez 1 « Pain ».");
    }

    [Fact]
    public void Choix_d_option_sur_la_ligne_menu_refuse()
    {
        var cart = Price(new CartLine(null, _c.Menu.Id, 1, [], [], [new CartOptionChoice(Pain("Pita").Id)]));

        Assert.Contains(cart.Issues, i => i.Message == "Un menu se personnalise produit par produit.");
    }

    [Fact]
    public void Double_jambon_et_un_oeuf_font_trois_ingredients()
    {
        var cart = Price(Quantities(_c.Pizza3.Id, (Garniture("Œuf"), 1), (Garniture("Jambon"), 2)));

        Assert.True(cart.IsValid, string.Join(" ", cart.Issues.Select(i => i.Message)));
        Assert.Equal(900, cart.TotalCents);
        var mods = cart.Items.Single().Modifications;
        Assert.Equal(["Ingrédients : Jambon", "Ingrédients : Œuf"], mods.Select(m => m.LabelSnapshot));
        Assert.Equal([2, 1], mods.Select(m => m.Quantity));
    }

    [Fact]
    public void Trois_fois_le_meme_choix_refuse_au_dela_du_maximum_par_choix()
    {
        var cart = Price(Quantities(_c.Pizza3.Id, (Garniture("Jambon"), 3)));

        Assert.False(cart.IsValid);
        Assert.Contains(cart.Issues, i => i.Message == "Pizza 3 : Jambon entre 1 et 2 fois.");
    }

    [Fact]
    public void Quantite_nulle_refusee()
    {
        var cart = Price(Quantities(_c.Pizza3.Id, (Garniture("Jambon"), 2), (Garniture("Œuf"), 1), (Garniture("Olives"), 0)));

        Assert.False(cart.IsValid);
    }

    [Fact]
    public void Les_unites_comptent_pour_le_minimum_et_le_maximum()
    {
        var tooFew = Price(Quantities(_c.Pizza3.Id, (Garniture("Jambon"), 2)));
        var tooMany = Price(Quantities(_c.Pizza3.Id, (Garniture("Jambon"), 2), (Garniture("Œuf"), 2)));

        Assert.Contains(tooFew.Issues, i => i.Message == "Pizza 3 : choisissez 3 « Ingrédients ».");
        Assert.Contains(tooMany.Issues, i => i.Message == "Pizza 3 : choisissez 3 « Ingrédients ».");
    }

    [Fact]
    public void Un_choix_unique_ne_se_double_pas()
    {
        var cart = Price(Quantities(_c.Kebab.Id, (Pain("Baguette"), 1), (Sauce("Blanche"), 2)));

        Assert.Contains(cart.Issues, i => i.Message == "Kebab : Blanche ne se prend qu'une fois.");
    }

    [Fact]
    public void Le_produit_redefinit_les_bornes_du_groupe_partage()
    {
        var five = Price(Quantities(_c.Pizza5.Id, (Garniture("Jambon"), 2), (Garniture("Œuf"), 2), (Garniture("Olives"), 1)));
        var three = Price(Quantities(_c.Pizza5.Id, (Garniture("Jambon"), 2), (Garniture("Œuf"), 1)));

        Assert.True(five.IsValid, string.Join(" ", five.Issues.Select(i => i.Message)));
        Assert.Equal(1100, five.TotalCents);
        Assert.Contains(three.Issues, i => i.Message == "Pizza 5 : entre 5 et 6 « Ingrédients ».");
    }

    [Fact]
    public void Choix_double_a_surcout_facture_a_chaque_fois_en_une_modification()
    {
        Garniture("Olives").ExtraPriceCents = 150;

        var cart = Price(Quantities(_c.Pizza5.Id, (Garniture("Olives"), 2), (Garniture("Jambon"), 2), (Garniture("Œuf"), 2)));

        Assert.True(cart.IsValid, string.Join(" ", cart.Issues.Select(i => i.Message)));
        Assert.Equal(1100 + 2 * 150, cart.TotalCents);
        var olives = cart.Items[0].Modifications.Single(m => m.LabelSnapshot == "Ingrédients : Olives");
        Assert.Equal((2, 150), (olives.Quantity, olives.UnitPriceCents));
    }

    [Fact]
    public void Un_choix_double_suffit_a_garder_le_produit_commandable()
    {
        _c.Olives.IsAvailable = false;
        _c.Oeuf.IsAvailable = false;

        // Jambon seul, 2 fois au plus : 2 unités < 3 exigées.
        Assert.False(AvailabilityPolicy.ForProduct(_c.Pizza3).IsOrderable);

        _c.Oeuf.IsAvailable = true;
        Assert.True(AvailabilityPolicy.ForProduct(_c.Pizza3).IsOrderable);
    }
}
