using RestaurantHandler.Models;

namespace RestaurantHandler.Domain.Policies;

/// <summary>
/// Choix dans les groupes d'options d'un produit (R4, docs/20-regles-metier/01-regles-commande.md) :
/// chaque groupe actif reçoit entre son minimum et son maximum d'unités, chaque choix proposé au
/// plus <see cref="OptionGroup.MaxPerChoice"/> fois (double jambon = 2 unités).
/// Prix d'un groupe = Σ surcoûts des unités + (unités − incluses)⁺ × prix d'un choix en plus.
/// </summary>
public static class OptionChoicePolicy
{
    /// <summary>
    /// Une modification par choix, groupes et choix dans l'ordre de la carte ; un choix à cheval
    /// sur la limite des unités incluses est scindé en une part incluse et une part payante. Les
    /// unités payantes sont les dernières dans l'ordre de la carte : le total ne dépend pas de
    /// l'ordre de la requête. Renvoie null après avoir signalé chaque écart.
    /// </summary>
    public static List<OrderItemModification>? Build(
        Product product,
        IReadOnlyCollection<CartOptionChoice> requested,
        Action<string> report
    )
    {
        var groups = AvailabilityPolicy.ActiveOptionGroups(product).ToList();
        var byId = groups
            .SelectMany(g => g.Group.Choices.Where(c => c.ArchivedAt is null))
            .ToDictionary(c => c.Id);

        var ok = true;
        if (requested.Select(c => c.ChoiceId).Distinct().Count() != requested.Count)
        {
            report($"{product.Name} : un choix ne figure qu'une fois (utilisez la quantité).");
            ok = false;
        }

        if (requested.Any(c => !byId.ContainsKey(c.ChoiceId)))
        {
            report($"{product.Name} : ce choix n'est pas proposé.");
            ok = false;
        }

        var wanted = requested
            .GroupBy(c => c.ChoiceId)
            .ToDictionary(g => g.Key, g => g.First().Quantity);
        var modifications = new List<OrderItemModification>();
        foreach (var offered in groups)
        {
            var group = offered.Group;
            var chosen = group.Choices
                .Where(c => c.ArchivedAt is null && wanted.ContainsKey(c.Id))
                .OrderBy(c => c.Position).ThenBy(c => c.Name)
                .Select(c => (Choice: c, Quantity: wanted[c.Id]))
                .ToList();

            var invalid = chosen.Where(c => c.Quantity < 1 || c.Quantity > group.MaxPerChoice).ToList();
            foreach (var (choice, _) in invalid)
                report(group.MaxPerChoice == 1
                    ? $"{product.Name} : {choice.Name} ne se prend qu'une fois."
                    : $"{product.Name} : {choice.Name} entre 1 et {group.MaxPerChoice} fois.");
            if (invalid.Count > 0)
            {
                ok = false;
                continue;
            }

            var units = chosen.Sum(c => c.Quantity);
            if (units < offered.MinChoices || units > offered.MaxChoices)
            {
                report(BoundsMessage(product, offered));
                ok = false;
                continue;
            }

            var counted = 0;
            foreach (var (choice, quantity) in chosen)
            {
                if (!AvailabilityPolicy.IsOptionChoiceOffered(choice))
                {
                    report($"{product.Name} : {choice.Name} est en rupture.");
                    ok = false;
                    continue;
                }

                var included = Math.Clamp(offered.FreeChoices - counted, 0, quantity);
                counted += quantity;
                var paid = quantity - included;
                if (group.ExtraChoicePriceCents == 0)
                    (included, paid) = (quantity, 0);

                if (included > 0)
                    modifications.Add(ChoiceModification(group, choice, included, choice.ExtraPriceCents));
                if (paid > 0)
                    modifications.Add(ChoiceModification(group, choice, paid, choice.ExtraPriceCents + group.ExtraChoicePriceCents));
            }
        }

        return ok ? modifications : null;
    }

    private static OrderItemModification ChoiceModification(OptionGroup group, OptionChoice choice, int quantity, int unitPriceCents) =>
        new()
        {
            Type = ModificationType.Choice,
            OptionChoiceId = choice.Id,
            IngredientId = choice.IngredientId,
            LabelSnapshot = $"{group.Label} : {choice.Name}",
            Quantity = quantity,
            UnitPriceCents = unitPriceCents,
        };

    private static string BoundsMessage(Product product, OfferedOptionGroup offered) =>
        (offered.MinChoices, offered.MaxChoices) switch
        {
            var (min, max) when min == max => $"{product.Name} : choisissez {min} « {offered.Group.Label} ».",
            (0, var max) => $"{product.Name} : au plus {max} « {offered.Group.Label} ».",
            var (min, max) => $"{product.Name} : entre {min} et {max} « {offered.Group.Label} ».",
        };
}
