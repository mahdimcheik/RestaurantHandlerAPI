using RestaurantHandler.Models;

namespace RestaurantHandler.Domain.Policies;

public sealed record CartModification(ModificationType Type, Guid IngredientId, Guid? SubstituteIngredientId = null, int Quantity = 1);

public sealed record CartMenuChoice(
    Guid SlotId,
    Guid ProductId,
    IReadOnlyList<CartModification> Modifications,
    IReadOnlyList<Guid>? OptionChoiceIds = null
);

/// <summary>Ce que le client veut ; jamais un prix (R2).</summary>
public sealed record CartLine(
    Guid? ProductId,
    Guid? MenuId,
    int Quantity,
    IReadOnlyList<CartModification> Modifications,
    IReadOnlyList<CartMenuChoice> Choices,
    IReadOnlyList<Guid>? OptionChoiceIds = null
);

/// <summary>Écart sur une ligne du panier (index dans la requête) ; la commande est refusée.</summary>
public sealed record CartIssue(int LineIndex, string Message);

public sealed record PricedCart(IReadOnlyList<OrderItem> Items, int TotalCents, IReadOnlyList<CartIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

/// <summary>
/// Construit les lignes de commande à partir du panier et de la carte en vigueur : disponibilité
/// (R6), personnalisations (R4), menus (R5), snapshots (R3) et prix (R2). La carte attendue est
/// le graphe complet (composition, substitutions, suppléments, groupes d'options, catégorie ; menus avec emplacements
/// non archivés et options reliées à leurs produits).
/// </summary>
public static class CartPolicy
{
    public const int MaxLineQuantity = 20;
    public const int MaxLines = 50;

    public static PricedCart Price(
        IReadOnlyList<CartLine> lines,
        IReadOnlyDictionary<Guid, Product> products,
        IReadOnlyDictionary<Guid, Menu> menus
    )
    {
        var issues = new List<CartIssue>();
        var items = new List<OrderItem>();

        if (lines.Count == 0)
            issues.Add(new CartIssue(-1, "Le panier est vide."));
        if (lines.Count > MaxLines)
            issues.Add(new CartIssue(-1, $"Au plus {MaxLines} lignes par commande."));

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var report = (string message) => issues.Add(new CartIssue(index, message));

            if (line.Quantity < 1 || line.Quantity > MaxLineQuantity)
            {
                report($"Quantité comprise entre 1 et {MaxLineQuantity}.");
                continue;
            }

            OrderItem? item = (line.ProductId, line.MenuId) switch
            {
                ({ } productId, null) => BuildProduct(productId, line.Modifications, line.OptionChoiceIds ?? [], products, report),
                (null, { } menuId) => BuildMenu(menuId, line, products, menus, report),
                _ => Fail(report, "Une ligne désigne soit un produit, soit un menu."),
            };

            if (item is null)
                continue;
            item.Quantity = line.Quantity;
            items.Add(item);
        }

        return issues.Count > 0
            ? new PricedCart([], 0, issues)
            : new PricedCart(items, items.Sum(OrderPricing.LineTotal), []);
    }

    private static OrderItem? Fail(Action<string> report, string message)
    {
        report(message);
        return null;
    }

    private static OrderItem? BuildMenu(
        Guid menuId,
        CartLine line,
        IReadOnlyDictionary<Guid, Product> products,
        IReadOnlyDictionary<Guid, Menu> menus,
        Action<string> report
    )
    {
        if (line.Modifications.Count > 0 || line.OptionChoiceIds is { Count: > 0 })
            return Fail(report, "Un menu se personnalise produit par produit.");
        if (!menus.TryGetValue(menuId, out var menu) || !AvailabilityPolicy.IsMenuListed(menu))
            return Fail(report, "Ce menu n'est plus proposé.");

        var item = new OrderItem
        {
            MenuId = menu.Id,
            NameSnapshot = menu.Name,
            UnitPriceCents = menu.PriceCents,
            VatRate = menu.VatRate,
        };

        var slots = menu.Slots.Where(s => s.ArchivedAt is null).ToDictionary(s => s.Id);
        foreach (var unknown in line.Choices.Where(c => !slots.ContainsKey(c.SlotId)))
            report($"{menu.Name} : emplacement inconnu.");

        var ok = true;
        foreach (var slot in slots.Values.OrderBy(s => s.Position))
        {
            var choices = line.Choices.Where(c => c.SlotId == slot.Id).ToList();
            if (choices.Count < slot.MinChoices || choices.Count > slot.MaxChoices)
            {
                report(slot.MinChoices == slot.MaxChoices
                    ? $"{menu.Name} : choisissez {slot.MinChoices} « {slot.Label} »."
                    : $"{menu.Name} : entre {slot.MinChoices} et {slot.MaxChoices} « {slot.Label} ».");
                ok = false;
                continue;
            }

            foreach (var choice in choices)
            {
                var option = slot.Options.FirstOrDefault(o => o.ProductId == choice.ProductId);
                if (option is null)
                {
                    report($"{menu.Name} : ce choix n'est pas proposé pour « {slot.Label} ».");
                    ok = false;
                    continue;
                }

                var component = BuildProduct(
                    choice.ProductId,
                    choice.Modifications,
                    choice.OptionChoiceIds ?? [],
                    products,
                    m => report($"{menu.Name} : {m}")
                );
                if (component is null)
                {
                    ok = false;
                    continue;
                }

                // Taux unique du menu en attendant la réponse sur la ventilation (question 3).
                component.MenuSlotId = slot.Id;
                component.UnitPriceCents = option.ExtraPriceCents;
                component.VatRate = menu.VatRate;
                component.Quantity = 1;
                item.Children.Add(component);
            }
        }

        return ok && line.Choices.All(c => slots.ContainsKey(c.SlotId)) ? item : null;
    }

    private static OrderItem? BuildProduct(
        Guid productId,
        IReadOnlyList<CartModification> modifications,
        IReadOnlyList<Guid> optionChoiceIds,
        IReadOnlyDictionary<Guid, Product> products,
        Action<string> report
    )
    {
        if (!products.TryGetValue(productId, out var product))
            return Fail(report, "Produit introuvable.");

        var availability = AvailabilityPolicy.ForProduct(product);
        if (!availability.IsOrderable)
            return Fail(report, $"{product.Name} n'est plus disponible.");

        var item = new OrderItem
        {
            ProductId = product.Id,
            NameSnapshot = product.Name,
            UnitPriceCents = product.PriceCents,
            VatRate = product.VatRate,
        };

        var ok = true;
        var touched = new HashSet<Guid>();
        var supplements = new HashSet<Guid>();

        foreach (var mod in modifications)
        {
            string? error = null;
            OrderItemModification? built = null;

            switch (mod.Type)
            {
                case ModificationType.Choice:
                    error = "un choix d'option passe par la liste des choix.";
                    break;

                case ModificationType.Remove or ModificationType.Replace when !touched.Add(mod.IngredientId):
                    error = "un ingrédient ne peut être retiré ou remplacé qu'une fois.";
                    break;

                case ModificationType.Remove:
                {
                    var component = product.Ingredients.FirstOrDefault(pi => pi.IngredientId == mod.IngredientId);
                    if (component is null || !component.IsRemovable)
                        error = "cet ingrédient ne peut pas être retiré.";
                    else
                        built = new OrderItemModification
                        {
                            Type = ModificationType.Remove,
                            IngredientId = mod.IngredientId,
                            LabelSnapshot = $"Sans {component.Ingredient?.Name}",
                            Quantity = 1,
                            UnitPriceCents = 0,
                        };
                    break;
                }

                case ModificationType.Replace:
                {
                    var substitution = product.Ingredients
                        .FirstOrDefault(pi => pi.IngredientId == mod.IngredientId)?
                        .Substitutions.FirstOrDefault(s => s.SubstituteIngredientId == mod.SubstituteIngredientId);
                    if (substitution is null)
                        error = "ce remplacement n'est pas proposé.";
                    else if (!AvailabilityPolicy.IsSubstitutionOffered(substitution))
                        error = $"{substitution.SubstituteIngredient?.Name} est en rupture.";
                    else
                    {
                        var original = product.Ingredients.First(pi => pi.IngredientId == mod.IngredientId).Ingredient?.Name;
                        built = new OrderItemModification
                        {
                            Type = ModificationType.Replace,
                            IngredientId = mod.IngredientId,
                            SubstituteIngredientId = substitution.SubstituteIngredientId,
                            LabelSnapshot = $"{original} → {substitution.SubstituteIngredient?.Name}",
                            Quantity = 1,
                            UnitPriceCents = substitution.ExtraPriceCents,
                        };
                    }
                    break;
                }

                case ModificationType.Supplement:
                {
                    var supplement = product.Supplements.FirstOrDefault(s => s.IngredientId == mod.IngredientId);
                    if (!supplements.Add(mod.IngredientId))
                        error = "un supplément ne figure qu'une fois (utilisez la quantité).";
                    else if (supplement is null)
                        error = "ce supplément n'est pas proposé.";
                    else if (!AvailabilityPolicy.IsSupplementOffered(supplement))
                        error = $"le supplément {supplement.Ingredient?.Name} est en rupture.";
                    else if (mod.Quantity < 1 || mod.Quantity > supplement.MaxQuantity)
                        error = $"supplément {supplement.Ingredient?.Name} : quantité entre 1 et {supplement.MaxQuantity}.";
                    else
                        built = new OrderItemModification
                        {
                            Type = ModificationType.Supplement,
                            IngredientId = mod.IngredientId,
                            LabelSnapshot = $"+ {supplement.Ingredient?.Name}",
                            Quantity = mod.Quantity,
                            UnitPriceCents = supplement.PriceCents,
                        };
                    break;
                }
            }

            if (error is not null)
            {
                report($"{product.Name} : {error}");
                ok = false;
            }
            else if (built is not null)
            {
                item.Modifications.Add(built);
            }
        }

        // Rupture d'un ingrédient retirable : le client doit voir et accepter le retrait (ou un remplacement).
        foreach (var forced in availability.ForcedRemovalIngredientIds.Where(id => !touched.Contains(id)))
        {
            var name = product.Ingredients.First(pi => pi.IngredientId == forced).Ingredient?.Name;
            report($"{name} est en rupture : {product.Name} sera servi sans. Mettez à jour votre panier.");
            ok = false;
        }

        var choices = OptionChoicePolicy.Build(product, optionChoiceIds, report);
        if (choices is null)
            ok = false;
        else
            foreach (var choice in choices)
                item.Modifications.Add(choice);

        var position = 0;
        foreach (var modification in item.Modifications)
            modification.Position = position++;

        return ok ? item : null;
    }
}

/// <summary>Montants d'une ligne à partir des snapshots (R2) : seule source de calcul, à la validation comme à la relecture.</summary>
public static class OrderPricing
{
    /// <summary>Prix unitaire d'une ligne : base + personnalisations + composants de menu.</summary>
    public static int UnitTotal(OrderItem item) =>
        item.UnitPriceCents
        + item.Modifications.Sum(m => m.UnitPriceCents * m.Quantity)
        + item.Children.Sum(UnitTotal);

    public static int LineTotal(OrderItem item) => UnitTotal(item) * item.Quantity;
}
