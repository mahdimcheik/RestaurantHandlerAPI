using RestaurantHandler.Models.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models;

public class TypeAddress : TranslatableModelOption
{
    [MaxLength(64)]
    public string DisplayName { get; set; }
}
