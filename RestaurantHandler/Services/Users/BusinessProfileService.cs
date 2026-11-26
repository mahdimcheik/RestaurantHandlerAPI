using Microsoft.EntityFrameworkCore;
using RestaurantHandler.Contexts;
using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Services;

/// <summary>
/// Profil professionnel du client connecté (US-E01-4, US-E05-6) : le renseigner fait du client un
/// professionnel qui peut demander une facture ; le supprimer le ramène à un compte particulier.
/// Les demandes de facture déjà créées gardent leur propre copie des informations.
/// </summary>
public class BusinessProfileService(MainContext db, TimeProvider clock)
{
    /// <summary>Null si le client n'est pas professionnel.</summary>
    public async Task<BusinessProfileDetails?> GetMineAsync(CallerIdentity caller)
    {
        var profile = await db.BusinessProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == caller.UserId);
        return profile is null ? null : BusinessProfileDetails.From(profile);
    }

    public async Task<BusinessProfileDetails> UpsertMineAsync(BusinessProfileUpsert dto, CallerIdentity caller)
    {
        var siren = BusinessIdentityPolicy.Normalize(dto.Siren);
        var siret = NullIfEmpty(BusinessIdentityPolicy.Normalize(dto.Siret));
        var vat = NullIfEmpty(BusinessIdentityPolicy.Normalize(dto.VatNumber));
        if (BusinessIdentityPolicy.Check(siren, siret, vat) is { } error)
            throw new BusinessRuleException(error);

        var now = clock.GetUtcNow();
        var profile = await db.BusinessProfiles.FirstOrDefaultAsync(p => p.UserId == caller.UserId);
        if (profile is null)
        {
            profile = new BusinessProfile { UserId = caller.UserId, CreatedAt = now };
            db.BusinessProfiles.Add(profile);
        }
        else
        {
            profile.UpdatedAt = now;
        }

        profile.Siren = siren;
        profile.Siret = siret;
        profile.VatNumber = vat ?? BusinessIdentityPolicy.FrenchVatNumber(siren);
        profile.LegalName = dto.LegalName.Trim();
        profile.AddressLine1 = dto.AddressLine1.Trim();
        profile.AddressLine2 = NullIfEmpty(dto.AddressLine2?.Trim());
        profile.PostalCode = dto.PostalCode.Trim();
        profile.City = dto.City.Trim();
        profile.CountryCode = "FR";

        await db.SaveChangesAsync();
        return BusinessProfileDetails.From(profile);
    }

    public async Task DeleteMineAsync(CallerIdentity caller)
    {
        var deleted = await db.BusinessProfiles.Where(p => p.UserId == caller.UserId).ExecuteDeleteAsync();
        if (deleted == 0)
            throw new ResourceNotFoundException("Aucun profil professionnel à supprimer.");
    }

    public Task<bool> ExistsAsync(Guid userId) => db.BusinessProfiles.AnyAsync(p => p.UserId == userId);

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
