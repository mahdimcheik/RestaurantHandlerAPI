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
    
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
}
