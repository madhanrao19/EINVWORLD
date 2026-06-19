-- Idempotent apply script for migration 20260619010000_AddInvoiceHotPathIndexes.
-- Bounds RefDocumentNo / InvoiceDirection (from nvarchar(max)) so they can be indexed, then adds
-- indexes on the hot lookup/filter columns used by the status sync, search, and detail pages.
-- Safe to run more than once.
BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619010000_AddInvoiceHotPathIndexes'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ALTER COLUMN [RefDocumentNo]    nvarchar(200) NULL;
    ALTER TABLE [InvoiceHeaders] ALTER COLUMN [InvoiceDirection] nvarchar(50)  NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InvoiceHeaders_CreatedDate' AND object_id = OBJECT_ID(N'[InvoiceHeaders]'))
        CREATE INDEX [IX_InvoiceHeaders_CreatedDate] ON [InvoiceHeaders] ([CreatedDate]);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InvoiceHeaders_InvoiceDirection' AND object_id = OBJECT_ID(N'[InvoiceHeaders]'))
        CREATE INDEX [IX_InvoiceHeaders_InvoiceDirection] ON [InvoiceHeaders] ([InvoiceDirection]);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InvoiceHeaders_RefDocumentNo' AND object_id = OBJECT_ID(N'[InvoiceHeaders]'))
        CREATE INDEX [IX_InvoiceHeaders_RefDocumentNo] ON [InvoiceHeaders] ([RefDocumentNo]);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InvoiceHeaders_UUID' AND object_id = OBJECT_ID(N'[InvoiceHeaders]'))
        CREATE INDEX [IX_InvoiceHeaders_UUID] ON [InvoiceHeaders] ([UUID]);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InvoiceHistories_InvoiceNo' AND object_id = OBJECT_ID(N'[InvoiceHistories]'))
        CREATE INDEX [IX_InvoiceHistories_InvoiceNo] ON [InvoiceHistories] ([InvoiceNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619010000_AddInvoiceHotPathIndexes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260619010000_AddInvoiceHotPathIndexes', N'10.0.9');
END;

COMMIT;
GO
