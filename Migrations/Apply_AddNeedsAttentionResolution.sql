-- Idempotent apply script for migration 20260930010000_AddNeedsAttentionResolution.
--
-- Lets Invalid / reject-requested invoices leave the "Needs Attention" list:
--   - InvoiceHeaders.AttentionResolvedAt / AttentionResolvedBy: set when a user marks the invoice
--     as resolved (display-only; LHDN/internal statuses are untouched).
--   - InvoiceHeaders.ResentFromInvoiceNo (+ index): the invoice a "Resend" copy was cloned from.
--     Once the copy is LHDN Valid, the original automatically drops out of Needs Attention.
--
-- All new columns are nullable and additive; no existing row is touched. Safe to re-run.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;

IF COL_LENGTH('InvoiceHeaders', 'AttentionResolvedAt') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [AttentionResolvedAt] datetime2 NULL;
END

IF COL_LENGTH('InvoiceHeaders', 'AttentionResolvedBy') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [AttentionResolvedBy] nvarchar(256) NULL;
END

IF COL_LENGTH('InvoiceHeaders', 'ResentFromInvoiceNo') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [ResentFromInvoiceNo] nvarchar(50) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_InvoiceHeaders_ResentFromInvoiceNo' AND [object_id] = OBJECT_ID(N'[InvoiceHeaders]'))
BEGIN
    CREATE INDEX [IX_InvoiceHeaders_ResentFromInvoiceNo] ON [InvoiceHeaders] ([ResentFromInvoiceNo]);
END

-- ── History row ──────────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260930010000_AddNeedsAttentionResolution')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260930010000_AddNeedsAttentionResolution', N'10.0.11');
END;

COMMIT;
GO
