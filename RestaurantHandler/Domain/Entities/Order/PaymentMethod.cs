using RestaurantHandler.Models.Interfaces;

namespace RestaurantHandler.Models;

/// <summary>
/// ROLE: Lookup/enum table for how a <see cref="Payment"/> was made
/// (e.g. Card, Wallet, Bank transfer). Inherits Name/Color/Icon from
/// <see cref="BaseModelOption"/> so it can be shown directly in the UI.
/// </summary>
public class PaymentMethod : TranslatableModelOption
{
}
