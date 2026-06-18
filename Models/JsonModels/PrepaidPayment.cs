using eInvWorld.Models.JsonModels;

namespace eInvWorld.Models.JsonModels
{
    public class PrepaidPayment
    {
        public List<ID> ID { get; set; } = new();
        public List<PaidAmount> PaidAmount { get; set; } = new();
        public List<PaidDate> PaidDate { get; set; } = new();
        public List<PaidTime> PaidTime { get; set; } = new();
    }
}
