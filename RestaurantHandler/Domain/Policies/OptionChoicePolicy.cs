using RestaurantHandler.Models;

namespace RestaurantHandler.Domain.Policies;

/// <summary>
/// Choix dans les groupes d'options d'un produit (R4, docs/20-regles-metier/01-regles-commande.md) :
/// chaque groupe actif reçoit entre son minimum et son maximum de choix distincts et proposés.
/// Prix d'un groupe = Σ surcoûts des choix + (nombre − gratuits)⁺ × prix d'un choix en plus.
/// </summary>
public static class OptionChoicePolicy
{
    /// <summary>
    /// Une modification par choix, groupes et choix dans l'ordre de la carte. Le prix d'un choix
    /// au-delà des gratuits est porté par les derniers choix dans cet ordre : le total ne dépend
    /// pas de l'ordre de la requête. Renvoie null après avoir signalé chaque écart.
    /// </summary>
    public static List<OrderItemModification>? Build(
        Product product,
        IReadOnlyCollection<Guid> choiceIds,
        Action<string> report
    )
    {
        var groups = AvailabilityPolicy.ActiveOptionGroups(product).ToList();
        var byId = groups
            .SelectMany(g => g.Choices.Where(c => c.ArchivedAt is null))
            .ToDictionary(c => c.Id);

        var ok = true;
        if (choiceIds.Distinct().Count() != choiceIds.Count)
        {
            report($"{product.Name} : un choix ne figure qu'une fois.");
            ok = false;
        }

        if (choiceIds.Any(id => !byId.ContainsKey(id)))
        {
            report($"{product.Name} : ce choix n'est pas proposé.");
            ok = false;
        }

        var wanted = choiceIds.ToHashSet();
        var modifications = new List<OrderItemModification>();
        foreach (var group in groups)
        {
            var chosen = group.Choices
                .Where(c => c.ArchivedAt is null && wanted.Contains(c.Id))
                .OrderBy(c => c.Position).ThenBy(c => c.Name)
                .ToList();

            if (chosen.Count < group.MinChoices || chosen.Count > group.MaxChoices)
            {
                report(BoundsMessage(product, group));
                ok = false;
                continue;
            }

            for (var index = 0; index < chosen.Count; index++)
            {
                var choice = chosen[index];
                if (!AvailabilityPolicy.IsOptionChoiceOffered(choice))
                {
                    report($"{product.Name} : {choice.Name} est en rupture.");
                    ok = false;
                    continue;
                }

                modifications.Add(new OrderItemModification
                {
                    Type = ModificationType.Choice,
                    OptionChoiceId = choice.Id,
                    IngredientId = choice.IngredientId,
                    LabelSnapshot = $"{group.Label} : {choice.Name}",
                    Quantity = 1,
                    UnitPriceCents = choice.ExtraPriceCents
                        + (index >= group.FreeChoices ? group.ExtraChoicePriceCents : 0),
                });
            }
        }

        return ok ? modifications : null;
    }

    private static string BoundsMessage(Product product, OptionGroup group) =>
        (group.MinChoices, group.MaxChoices) switch
        {
            var (min, max) when min == max => $"{product.Name} : choisissez {min} « {group.Label} ».",
            (0, var max) => $"{product.Name} : au plus {max} « {group.Label} ».",
            var (min, max) => $"{product.Name} : entre {min} et {max} « {group.Label} ».",
        };
}
