using RestaurantHandler.Models.Interfaces;

namespace RestaurantHandler.Models;

/// <summary>
/// ROLE: Lookup table for the lifecycle state of a <see cref="Dispute"/>
/// (Open, Resolved). UI-friendly via <see cref="BaseModelOption"/>.
/// </summary>
public class DisputeStatus : TranslatableModelOption
{
}
