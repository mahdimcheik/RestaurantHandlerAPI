using RestaurantHandler.Models.Files;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Models.Developpement
{
    public class GenApi
    {
        public RoleEnum RoleEnum { get; set; }
        public GenderEnum GenderEnum { get; set; }
        public AccountStatusEnum AccountStatusEnum { get; set; }
        public DocumentTypeEnum DocumentTypeEnum { get; set; }
        public SignalRNotificationTypeEnum SignalRNotificationTypeEnum { get; set; }
        public NotificationTypeEnum NotificationTypeEnum { get; set; }
    }
}
