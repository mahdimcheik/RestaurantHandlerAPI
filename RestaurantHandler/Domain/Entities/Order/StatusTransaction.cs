using RestaurantHandler.Models.Interfaces;

namespace RestaurantHandler.Models;

/// <summary>
/// ROLE: Lookup table for the state of a money transfer, currently used by
/// <see cref="TeacherPayout"/> (e.g. Pending, Processing, Paid, Failed).
/// UI-friendly via <see cref="BaseModelOption"/>.
/// </summary>
public class StatusTransaction : TranslatableModelOption
{
}
