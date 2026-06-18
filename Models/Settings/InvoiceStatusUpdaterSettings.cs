namespace eInvWorld.Models.Settings
{
    public class InvoiceStatusUpdaterSettings
    {
        public bool Enabled { get; set; } = true;
        public int PollingIntervalSeconds { get; set; } = 30;
    }
}
