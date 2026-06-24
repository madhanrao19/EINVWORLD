-- Idempotent apply script for migration 20260625000000_AddInvoiceStatusSyncIndexes.
-- Adds a composite index on InvoiceHeaders (LHDNStatusId, LastUpdated) for the status-sync poller.
-- Safe to run more than once.
BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260625000000_AddInvoiceStatusSyncIndexes'
)
BEGIN
    IF NOT EXISTS (
        SELECT * FROM sys.indexes
        WHERE name = N'IX_InvoiceHeaders_LHDNStatusId_LastUpdated'
          AND object_id = OBJECT_ID(N'[InvoiceHeaders]')
    )
    BEGIN
        CREATE INDEX [IX_InvoiceHeaders_LHDNStatusId_LastUpdated]
            ON [InvoiceHeaders] ([LHDNStatusId], [LastUpdated]);
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260625000000_AddInvoiceStatusSyncIndexes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260625000000_AddInvoiceStatusSyncIndexes', N'10.0.9');
END;

COMMIT;
GO
