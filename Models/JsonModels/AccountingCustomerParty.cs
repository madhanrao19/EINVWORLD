namespace eInvWorld.Models.JsonModels
{
    public class AccountingCustomerParty
    {
        public List<Party> Party { get; set; }

        public AccountingCustomerParty()
        {
            Party = new List<Party>();
        }
    }

}
