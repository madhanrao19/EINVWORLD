-- Idempotent apply script for migration 20260802130000_AddNewInvoiceReceivedEmailTrackingToInvoiceHeader.
--
-- Adds "new e-invoice received" notification tracking to InvoiceHeaders, mirroring the existing
-- IsValidationEmailSent/ValidationEmailSentAt/ValidationEmailSentTo columns. DEFAULT 1 (true) on
-- IsNewInvoiceReceivedEmailSent backfills every existing row as "not applicable" so this migration
-- never retroactively emails anyone about invoices already in the database — only invoices created
-- afterward by InvoiceFullSyncHelper for a genuinely new buyer-side sync from LHDN start out false
-- (eligible) and get picked up by the InvoiceStatusUpdater/InvoiceFinalizer background retry pass.
--
-- Safe to re-run.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;

IF COL_LENGTH('InvoiceHeaders', 'IsNewInvoiceReceivedEmailSent') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [IsNewInvoiceReceivedEmailSent] bit NOT NULL CONSTRAINT [DF_InvoiceHeaders_IsNewInvoiceReceivedEmailSent] DEFAULT 1;
END

IF COL_LENGTH('InvoiceHeaders', 'NewInvoiceReceivedEmailSentAt') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [NewInvoiceReceivedEmailSentAt] datetime2 NULL;
END

IF COL_LENGTH('InvoiceHeaders', 'NewInvoiceReceivedEmailSentTo') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [NewInvoiceReceivedEmailSentTo] nvarchar(500) NULL;
END

-- ── History row ──────────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260802130000_AddNewInvoiceReceivedEmailTrackingToInvoiceHeader')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260802130000_AddNewInvoiceReceivedEmailTrackingToInvoiceHeader', N'10.0.10');
END;

COMMIT;
GO
