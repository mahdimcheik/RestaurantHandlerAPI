using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Utilities;

/// <summary>Taux de TVA de la liste fermée (docs/20-regles-metier/04-conformite-legale.md).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class AllowedVatRateAttribute : ValidationAttribute
{
    public static readonly decimal[] Rates = [5.5m, 10m, 20m];

    public AllowedVatRateAttribute()
        : base("Taux de TVA invalide : 5,5 %, 10 % ou 20 %.") { }

    public override bool IsValid(object? value) => value is decimal rate && Rates.Contains(rate);
}
