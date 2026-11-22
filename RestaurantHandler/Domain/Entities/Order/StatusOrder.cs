using RestaurantHandler.Models.Interfaces;

namespace RestaurantHandler.Models;

/// <summary>
/// ROLE: Lookup table for the lifecycle state of an <see cref="Order"/>
/// (e.g. Pending, Paid, Cancelled). UI-friendly via <see cref="BaseModelOption"/>.
/// </summary>
public class StatusOrder : TranslatableModelOption
{
}