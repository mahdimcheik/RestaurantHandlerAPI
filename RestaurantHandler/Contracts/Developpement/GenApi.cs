using RestaurantHandler.Utilities;

namespace RestaurantHandler.Models.Developpement
{
    public class GenApi
    {
        public RoleEnum RoleEnum { get; set; }
        public AccountStatusEnum AccountStatusEnum { get; set; }
        public SignalRNotificationTypeEnum SignalRNotificationTypeEnum { get; set; }
        public NotificationTypeEnum NotificationTypeEnum { get; set; }
    }
}
