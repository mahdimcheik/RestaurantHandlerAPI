using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantHandler.Models.Interfaces;

namespace RestaurantHandler.Models;
public class Student: BaseModel
{
    [Required]
    [ForeignKey(nameof(User))]
    public Guid UserId { get; set; }
    
    public UserApp? User { get; set; }
    
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    
    public ICollection<Order> Orders { get; set; } = new List<Order>();

    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
}
