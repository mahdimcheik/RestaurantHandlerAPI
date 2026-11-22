using Microsoft.EntityFrameworkCore;
using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Services;


public class AddressesService(MainContext context, GeocodingService geocodingService)
{
    public async Task<Response<List<AddressDetails>>> GetAddressesByUserIdAsync(CallerIdentity? principal)
    {
        var user = CheckUser.GetUser(principal, context);
        if (user is null)
        {
            return new Response<List<AddressDetails>>
            {
                Status = 404,
                Message = $"L'utilisateur n'existe pas",
            };
        }
        var profile = await context.Users.FirstOrDefaultAsync(p => p.Id == user.Id);
        if (profile is null)
        {
            return new Response<List<AddressDetails>>
            {
                Status = 404,
                Message = $"L'utilisateur n'existe pas",
            };
        }

        var addresses = await context.Addresses
            .AsNoTracking()
            .Where(a => a.UserId == profile.Id)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AddressDetails(a, false))
            .ToListAsync();

        return new Response<List<AddressDetails>>
        {
            Status = 200,
            Message = "Adresses de l'utilisateur récupérées avec succès",
            Data = addresses,
            Count = addresses.Count
        };
    }

    public async Task<Response<AddressDetails>> CreateAddressAsync(AddressCreate addressDto, CallerIdentity? User)
    {
        // Vérifier que l'utilisateur existe
        var user = CheckUser.GetUser(User, context);
        if (user is null)
        {
            return new Response<AddressDetails>
            {
                Status = 404,
                Message = "Utilisateur non trouvé",
                Data = null
            };
        }
        var profile = await context.Users.FirstOrDefaultAsync(p => p.Id == user.Id);
        if (profile is null)
        {
            return new Response<AddressDetails>
            {
                Status = 404,
                Message = "Utilisateur non trouvé",
                Data = null
            };
        }
        if (addressDto.TypeId != HardCode.TYPE_ADDRESS_MAIN && addressDto.TypeId != HardCode.TYPE_ADDRESS_BILLING)
        {
            return new Response<AddressDetails>
            {
                Status = 400,
                Message = "Le type d'adresse est invalide",
                Data = null
            };
        }

        addressDto.UserId = profile.Id;

        // Chaque utilisateur n'a qu'une adresse par type (domicile / travail) :
        // soumettre à nouveau un type existant remplace l'adresse en place.
        var existingAddress = await context.Addresses
            .FirstOrDefaultAsync(a => a.UserId == profile.Id && a.TypeId == addressDto.TypeId);

        if (existingAddress != null)
        {
            existingAddress.UpdateAddress(new AddressUpdate
            {
                Id = existingAddress.Id,
                Street = addressDto.Street,
                City = addressDto.City,
                Country = addressDto.Country,
                ZipCode = addressDto.ZipCode,
                AdditionalInfo = addressDto.AdditionalInfo,
                Longitude = addressDto.Longitude,
                Latitude = addressDto.Latitude,
                TypeId = addressDto.TypeId,
            });

            var existingGeocoding = await geocodingService.GeocodeAsync(existingAddress.City, existingAddress.ZipCode);
            if (existingGeocoding.Success)
            {
                existingAddress.Latitude = existingGeocoding.Latitude;
                existingAddress.Longitude = existingGeocoding.Longitude;
            }

            await context.SaveChangesAsync();

            return new Response<AddressDetails>
            {
                Status = 200,
                Message = "Adresse mise à jour avec succès",
                Data = new AddressDetails(existingAddress)
            };
        }

        var address = new Address(addressDto);

        var geocoding = await geocodingService.GeocodeAsync(address.City, address.ZipCode);
        if (geocoding.Success)
        {
            address.Latitude = geocoding.Latitude;
            address.Longitude = geocoding.Longitude;
        }

        context.Addresses.Add(address);
        await context.SaveChangesAsync();

        return new Response<AddressDetails>
        {
            Status = 201,
            Message = "Adresse créée avec succès",
            Data = new AddressDetails(address)
        };
    }

    public async Task<Response<AddressDetails>> UpdateAddressAsync(AddressUpdate addressDto, CallerIdentity? User)
    {
        var user = CheckUser.GetUser(User, context);
        if (user is null)
        {
            return new Response<AddressDetails>
            {
                Status = 404,
                Message = "Utilisateur non trouvé",
                Data = null
            };
        }

        var address = await context.Addresses
            .FirstOrDefaultAsync(a =>
                a.Id == addressDto.Id && a.UserId == user.Id && a.ArchivedAt == null);

        if (address == null)
        {
            return new Response<AddressDetails>
            {
                Status = 404,
                Message = "Adresse non trouvée",
                Data = null
            };
        }
        if (addressDto.TypeId != HardCode.TYPE_ADDRESS_MAIN && addressDto.TypeId != HardCode.TYPE_ADDRESS_BILLING)
        {
            return new Response<AddressDetails>
            {
                Status = 400,
                Message = "Le type d'adresse est invalide",
                Data = null
            };
        }

        if (addressDto.TypeId != address.TypeId)
        {
            var conflictingAddress = await context.Addresses
                .AnyAsync(a => a.UserId == address.UserId && a.TypeId == addressDto.TypeId && a.Id != address.Id);

            if (conflictingAddress)
            {
                return new Response<AddressDetails>
                {
                    Status = 409,
                    Message = "Une adresse de ce type existe déjà pour cet utilisateur",
                    Data = null
                };
            }
        }

        address.UpdateAddress(addressDto);

        var geocoding = await geocodingService.GeocodeAsync(address.City, address.ZipCode);
        if (geocoding.Success)
        {
            address.Latitude = geocoding.Latitude;
            address.Longitude = geocoding.Longitude;
        }

        await context.SaveChangesAsync();
        return new Response<AddressDetails>
        {
            Status = 200,
            Message = "Adresse mise à jour avec succès",
            Data = new AddressDetails(address)
        };
    }

    public async Task<Response<object>> DeleteAddressAsync(Guid id, CallerIdentity? principal)
    {
        // Vérifier que l'utilisateur existe
        var user = CheckUser.GetUser(principal, context);
        if (user is null)
        {
            return new Response<object>
            {
                Status = 404,
                Message = "Utilisateur non trouvé",
            };
        }
        var profile = await context.Users.FirstOrDefaultAsync(p => p.Id == user.Id);
        if (profile is null)
        {
            return new Response<object>
            {
                Status = 404,
                Message = "Utilisateur non trouvé",
            };
        }
        var address = await context.Addresses
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == profile.Id);

        if (address == null)
        {
            return new Response<object>
            {
                Status = 404,
                Message = "Adresse non trouvée",
                Data = null
            };
        }

        address.ArchivedAt = DateTimeOffset.UtcNow;
        address.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync();

        return new Response<object>
        {
            Status = 200,
            Message = "Adresse supprimée avec succès",
            Data = null
        };
    }
}
