namespace eInvWorld.Models
{
    public class DeliveryAttempts
    {
        public DateTime? attemptDateTime { get; set; }
        public string status { get; set; } = null!;
        public string statusDetails { get; set; } = null!;
    }
}
