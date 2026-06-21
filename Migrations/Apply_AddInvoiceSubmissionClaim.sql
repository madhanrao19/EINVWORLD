-- Idempotent apply script for migration 20260621000000_AddInvoiceSubmissionClaim.
-- Adds the SubmissionClaimedAtUtc concurrency-claim column used to make the double-submit guard atomic.
-- Safe to run more than once.
BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621000000_AddInvoiceSubmissionClaim'
)
BEGIN
    IF COL_LENGTH(N'[InvoiceHeaders]', N'SubmissionClaimedAtUtc') IS NULL
        ALTER TABLE [InvoiceHeaders] ADD [SubmissionClaimedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621000000_AddInvoiceSubmissionClaim'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260621000000_AddInvoiceSubmissionClaim', N'10.0.9');
END;

COMMIT;
GO
