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
        // 7 days stays within GetAllUuidsForTinAsync's single 10-day date-chunk: 2 /documents/search calls
        // per cycle per TIN (one for Sent, one for Received documents).
        public int BackgroundImportLookbackDays { get; set; } = 7;

        // Cap on raw-document fetches (/documents/{uuid}/raw) per import run — background, Refresh from API
        // and the Admin import alike. 40 ≈ under a minute of LHDN's paced get-document queue, which
        // interactive status checks share. Anything left over is fetched by an automatic follow-up job.
        public int ImportMaxDocumentFetchesPerRun { get; set; } = EINVWORLD.Helpers.InvoiceSyncHelper.DefaultMaxDocumentFetchesPerRun;
    }
}
