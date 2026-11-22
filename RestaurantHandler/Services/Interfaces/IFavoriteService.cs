using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Services.Interfaces;

public interface IFavoriteService
{
    Task<Response<FavoriteDetails>> AddFavoriteAsync(CreateFavorite dto, CallerIdentity? principal);
    Task<Response<object>> RemoveFavoriteAsync(Guid teacherId, CallerIdentity? principal);
    Task<Response<bool>> IsFavoriteAsync(Guid teacherId, CallerIdentity? principal);
}
