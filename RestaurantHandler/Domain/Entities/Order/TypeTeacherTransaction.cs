using RestaurantHandler.Models.Interfaces;

namespace RestaurantHandler.Models;

/// <summary>
/// ROLE: Lookup table classifying a <see cref="TeacherWalletTransaction"/>
/// (e.g. Earning, Refund, Adjustment, Payout). Drives whether the entry
/// credits or debits the wallet. UI-friendly via <see cref="BaseModelOption"/>.
/// </summary>
public class TypeTeacherTransaction : TranslatableModelOption
{
}
