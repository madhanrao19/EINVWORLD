namespace eInvWorld.Models
{
    public class Notification
    {
        public string notificationId { get; set; } = null!;

        public DateTime? receivedDateTime { get; set; }

        public DateTime? deliveredDateTime { get; set; }

        public string typeId { get; set; } = null!;

        public string typeName { get; set; } = null!;

        public string finalMessage { get; set; } = null!;

        public string channel { get; set; } = null!;

        public string address { get; set; } = null!;

        public string language { get; set; } = null!;

        public string status { get; set; } = null!;

        public List<DeliveryAttempts> deliveryAttempts { get; set; } = new();

        public int totalPages { get; set; }

        public int totalCount { get; set; }
    }
}
