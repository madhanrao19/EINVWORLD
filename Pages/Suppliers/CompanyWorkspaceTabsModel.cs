namespace eInvWorld.Pages.Suppliers
{
    /// <summary>View model for the shared "My Company" tab bar (_CompanyWorkspaceTabs.cshtml).</summary>
    public class CompanyWorkspaceTabsModel
    {
        public int PartyInfoId { get; set; }

        /// <summary>"overview" or "profile" — which tab renders as active.</summary>
        public string Active { get; set; } = "overview";

        /// <summary>Preserves the existing "from=lead" query flow across tab links.</summary>
        public string? From { get; set; }
    }
}
