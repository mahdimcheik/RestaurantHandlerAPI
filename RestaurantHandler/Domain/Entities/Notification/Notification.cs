using RestaurantHandler.Models.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models
{
    public class Notification: TranslatableModelOption
    {
        [Required]
        public required string Message { get; set; }
        [Required]
        public bool IsSeen { get; set; }
        [Required]
        public Guid UserId { get; set; }
        public UserApp? User { get; set; }

        /// <summary>Chemin front (relatif) de l'écran concerné, ouvert au clic.</summary>
        [MaxLength(512)]
        public string? Link { get; set; }
    }
}
