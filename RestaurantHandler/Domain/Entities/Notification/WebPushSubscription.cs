using RestaurantHandler.Models.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models
{
    /// <summary>
    /// Abonnement Web Push d'un navigateur (un appareil) : ce que <c>PushManager.subscribe</c>
    /// renvoie côté front. Un utilisateur en a un par appareil où il a activé les notifications.
    /// L'endpoint est unique : un même navigateur repris par un autre compte est réassigné.
    /// </summary>
    [Index(nameof(Endpoint), IsUnique = true)]
    public class WebPushSubscription : BaseModel
    {
        [Required]
        public Guid UserId { get; set; }
        public UserApp? User { get; set; }

        [Required]
        [MaxLength(1024)]
        public required string Endpoint { get; set; }

        [Required]
        [MaxLength(256)]
        public required string P256dh { get; set; }

        [Required]
        [MaxLength(256)]
        public required string Auth { get; set; }

        [MaxLength(512)]
        public string? UserAgent { get; set; }

        public DateTimeOffset? LastUsedAt { get; set; }
    }
}
