namespace eInvWorld.Models.Settings
{
    public class InvoiceStatusUpdaterSettings
    {
        public bool Enabled { get; set; } = true;
        public int PollingIntervalSeconds { get; set; } = 30;

        // How far back the automatic LHDN full-document-search import (RunLhdnImportAsync, every
        // 10th poll cycle) looks for documents — catches invoices submitted by external ERPs that
        // never went through EINVWORLD. Kept separate from LHDNApiConfig:SyncRetentionDays, which is
        // a much longer one-time deep backfill window for the Admin-triggered manual import.
        // 7 days stays within GetAllUuidsForTinAsync's single 10-day date-chunk, so widening from the
        // previous 3-day default doesn't add extra /documents/search calls per cycle per TIN.
        public int BackgroundImportLookbackDays { get; set; } = 7;
    }
}
