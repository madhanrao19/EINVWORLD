-- Idempotent apply script for migration 20260803200000_AddRejectionCancellationEmailTrackingToInvoiceHeader.
--
-- Adds Rejection/Cancellation notification-email tracking to InvoiceHeaders, mirroring the existing
-- IsNewInvoiceReceivedEmailSent/NewInvoiceReceivedEmailSentAt/NewInvoiceReceivedEmailSentTo columns.
-- DEFAULT 1 (true) on IsRejectionEmailSent/IsCancellationEmailSent backfills every existing row as
-- "not applicable" so this migration never retroactively emails anyone about invoices already in the
-- database — only the reject/cancel handlers explicitly set these to false, in the same save as the
-- status transition, opting that one row into the InvoiceStatusUpdater/InvoiceFinalizer background
-- retry pass. CancellationReason is also added (Reject already has RejectedReason) so a background
-- retry of a failed cancellation email can rebuild the email body.
--
-- Safe to re-run.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;

IF COL_LENGTH('InvoiceHeaders', 'IsRejectionEmailSent') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [IsRejectionEmailSent] bit NOT NULL CONSTRAINT [DF_InvoiceHeaders_IsRejectionEmailSent] DEFAULT 1;
END

IF COL_LENGTH('InvoiceHeaders', 'RejectionEmailSentAt') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [RejectionEmailSentAt] datetime2 NULL;
END

IF COL_LENGTH('InvoiceHeaders', 'RejectionEmailSentTo') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [RejectionEmailSentTo] nvarchar(500) NULL;
END

IF COL_LENGTH('InvoiceHeaders', 'IsCancellationEmailSent') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [IsCancellationEmailSent] bit NOT NULL CONSTRAINT [DF_InvoiceHeaders_IsCancellationEmailSent] DEFAULT 1;
END

IF COL_LENGTH('InvoiceHeaders', 'CancellationEmailSentAt') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [CancellationEmailSentAt] datetime2 NULL;
END

IF COL_LENGTH('InvoiceHeaders', 'CancellationEmailSentTo') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [CancellationEmailSentTo] nvarchar(500) NULL;
END

IF COL_LENGTH('InvoiceHeaders', 'CancellationReason') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [CancellationReason] nvarchar(max) NULL;
END

-- ── History row ──────────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260803200000_AddRejectionCancellationEmailTrackingToInvoiceHeader')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260803200000_AddRejectionCancellationEmailTrackingToInvoiceHeader', N'10.0.10');
END;

COMMIT;
GO
