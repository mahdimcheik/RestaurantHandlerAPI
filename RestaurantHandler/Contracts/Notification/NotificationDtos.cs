namespace RestaurantHandler.Models
{
    public class NotificationDetails
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsSeen { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string? Link { get; set; }
    }

    public class PushSubscriptionKeys
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MaxLength(256)]
        public string P256dh { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MaxLength(256)]
        public string Auth { get; set; } = string.Empty;
    }

    /// <summary>Forme de <c>PushSubscription.toJSON()</c> du navigateur.</summary>
    public class PushSubscriptionCreate
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.Url]
        [System.ComponentModel.DataAnnotations.MaxLength(1024)]
        public string Endpoint { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required]
        public PushSubscriptionKeys Keys { get; set; } = new();
    }

    public class PushSubscriptionDelete
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MaxLength(1024)]
        public string Endpoint { get; set; } = string.Empty;
    }
}
