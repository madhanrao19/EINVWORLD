using System.Collections.Generic;

namespace eInvWorld.Models.JsonModels
{
    public class AccountingSupplierParty
    {
        // Change to nullable or just leave as is, but don't initialize as empty list
        public List<AdditionalAccountID>? AdditionalAccountID { get; set; }
        public List<Party> Party { get; set; }

        public AccountingSupplierParty()
        {
            AdditionalAccountID = new List<AdditionalAccountID>();
            Party = new List<Party>();
        }
    }
}