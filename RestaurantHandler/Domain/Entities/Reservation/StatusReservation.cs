using System.ComponentModel.DataAnnotations;
using RestaurantHandler.Models.Interfaces;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Models;

public class StatusReservation : TranslatableModelOption
{
    [Required]
    public StatusReservationCode Code { get; set; }
}
