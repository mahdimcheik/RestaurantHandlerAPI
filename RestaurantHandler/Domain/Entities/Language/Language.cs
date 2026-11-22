using RestaurantHandler.Models.Interfaces;

namespace RestaurantHandler.Models;

public class Language : TranslatableModelOption
{
    public ICollection<Teacher>? Teachers { get; set; }
}
