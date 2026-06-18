namespace eInvWorld.Models.JsonModels
{
    public class PaymentMeans
    {
        public List<PaymentMeansCode> PaymentMeansCode { get; set; } = new();
        public List<PayeeFinancialAccount> PayeeFinancialAccount { get; set; } = new();
    }
}
