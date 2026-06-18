using eInvWorld.Models.InputModel;

namespace eInvWorld.Models.ViewModels
{
    public class InvoiceFormViewModel
    {
        public PartyInfo PartyInfo { get; set; } = null!;

        // Initialize DocumentDetails and DocumentDetailTaxes to avoid null reference
        public List<DocumentDetailViewModel> DocumentDetails { get; set; } = new List<DocumentDetailViewModel>();
        public List<DocumentDetailTaxViewModel> DocumentDetailTaxes { get; set; } = new List<DocumentDetailTaxViewModel>();
    }

}
