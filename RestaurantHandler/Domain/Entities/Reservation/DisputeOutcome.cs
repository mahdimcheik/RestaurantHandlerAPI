using RestaurantHandler.Models.Interfaces;

namespace RestaurantHandler.Models;

/// <summary>
/// ROLE: Lookup table for the outcome of a resolved <see cref="Dispute"/>
/// (Teacher, Student, Partial). UI-friendly via <see cref="BaseModelOption"/>.
/// </summary>
public class DisputeOutcome : TranslatableModelOption
{
}
