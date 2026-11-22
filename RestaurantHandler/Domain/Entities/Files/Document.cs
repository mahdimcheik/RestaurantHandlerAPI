using RestaurantHandler.Models.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantHandler.Models;

[Table("Documents")]
public class Document : BaseModel
{
    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string MimeType { get; set; } = string.Empty;

    public long Size { get; set; }

    [Column(TypeName = "timestamp with time zone")]
    public DateTimeOffset UploadedAt { get; set; }

    [ForeignKey(nameof(Teacher))]
    public Guid? TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    [ForeignKey(nameof(Student))]
    public Guid? StudentId { get; set; }
    public Student? Student { get; set; }

    [ForeignKey(nameof(Reservation))]
    public Guid? ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    // Set only on invoice documents (the generated bill for a paid order).
    // Used as the idempotency key: at most one Document per Order.
    [ForeignKey(nameof(Order))]
    public Guid? OrderId { get; set; }
    public Order? Order { get; set; }

    // Parameterless constructor for EF Core
    public Document()
    {
    }
}

