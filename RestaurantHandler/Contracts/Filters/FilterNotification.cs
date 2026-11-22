namespace RestaurantHandler.Models
{
    public class FilterNotification
    {
        public bool? IsSeen { get; set; }
        public int First { get; set; } = 10;
        public int? Row { get; set; }
    }
}
