using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantHandler.Models.Interfaces;

namespace RestaurantHandler.Models;

public class Favorite : BaseModel
{
    [Required]
    [ForeignKey(nameof(Student))]
    public Guid StudentId { get; set; }

    public Student? Student { get; set; }

    [Required]
    [ForeignKey(nameof(Teacher))]
    public Guid TeacherId { get; set; }

    public Teacher? Teacher { get; set; }
}
