using eInvWorld.Models.Document;

namespace eInvWorld.Models
{
    public class NotificationResp
    {
        public List<Notification> result { get; set; } = new();
        public Metadata metadata { get; set; } = null!;
    }
}
