-- Idempotent apply script for migration 20260708000000_AddInvoiceHeaderRowVersion.
-- Adds a rowversion concurrency column to InvoiceHeaders (optimistic concurrency guard between the
-- background status sync and concurrent user actions). Purely additive; the engine populates the
-- column for existing rows automatically. Safe to run more than once.
BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260708000000_AddInvoiceHeaderRowVersion'
)
BEGIN
    IF NOT EXISTS (
        SELECT * FROM sys.columns
        WHERE object_id = OBJECT_ID(N'[dbo].[InvoiceHeaders]') AND name = N'RowVersion'
    )
    BEGIN
        ALTER TABLE [dbo].[InvoiceHeaders] ADD [RowVersion] rowversion NOT NULL;
    END

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260708000000_AddInvoiceHeaderRowVersion', N'10.0.9');
END

COMMIT;
