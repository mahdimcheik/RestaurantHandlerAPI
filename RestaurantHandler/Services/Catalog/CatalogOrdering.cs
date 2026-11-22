using RestaurantHandler.Infrastructure.Errors;

namespace RestaurantHandler.Services;

public static class CatalogOrdering
{
    /// <summary>
    /// Applique un ordre complet : la liste doit contenir exactement les éléments existants, sinon
    /// un ajout ou un archivage concurrent serait silencieusement mal placé.
    /// </summary>
    public static void Apply<T>(IReadOnlyCollection<T> items, Func<T, Guid> id, IReadOnlyList<Guid> orderedIds, Action<T, int> setPosition)
    {
        var byId = items.ToDictionary(id);
        if (orderedIds.Count != byId.Count || orderedIds.Distinct().Count() != orderedIds.Count || !orderedIds.All(byId.ContainsKey))
            throw new BusinessConflictException("La liste ne correspond pas aux éléments actuels : rechargez puis réessayez.");

        for (var i = 0; i < orderedIds.Count; i++)
            setPosition(byId[orderedIds[i]], i);
    }
}
