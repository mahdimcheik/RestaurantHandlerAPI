using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Utilities;

/// <summary>
/// Refuse les adresses email contenant un <c>+</c> (alias du type <c>nom+test@exemple.com</c>),
/// qui permettraient de créer plusieurs comptes avec une même boîte mail. Appliqué à la
/// création de compte uniquement : les comptes existants restent connectables.
/// Équivalent de <c>noPlusInEmailValidator()</c> côté Angular.
///
/// Un champ vide est considéré valide (laisser <see cref="RequiredAttribute"/> gérer l'obligation).
/// </summary>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter,
    AllowMultiple = false
)]
public sealed class NoPlusInEmailAttribute : ValidationAttribute
{
    public const string DefaultErrorMessage = "L'adresse email ne doit pas contenir le caractère +";

    public NoPlusInEmailAttribute()
        : base(DefaultErrorMessage) { }

    public static bool IsAllowed(string? email) => string.IsNullOrEmpty(email) || !email.Contains('+');

    public override bool IsValid(object? value) => IsAllowed(value as string);
}
