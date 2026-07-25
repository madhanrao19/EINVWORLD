-- Idempotent apply script for migration 20260725053929_AddSavedInvoiceViews.
-- Adds the SavedInvoiceViews table (Sent Invoices workspace "Saved Views" feature).
-- Purely additive; safe to run more than once.
BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725053929_AddSavedInvoiceViews'
)
BEGIN
    IF OBJECT_ID(N'[SavedInvoiceViews]', N'U') IS NULL
    BEGIN
        CREATE TABLE [SavedInvoiceViews] (
            [Id] int NOT NULL IDENTITY,
            [UserId] nvarchar(450) NOT NULL,
            [InvoiceDirection] nvarchar(20) NOT NULL,
            [Name] nvarchar(100) NOT NULL,
            [QueryString] nvarchar(2048) NOT NULL,
            [CreatedAtUtc] datetime2 NOT NULL,
            CONSTRAINT [PK_SavedInvoiceViews] PRIMARY KEY ([Id])
        );
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725053929_AddSavedInvoiceViews'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260725053929_AddSavedInvoiceViews', N'10.0.9');
END;

COMMIT;
GO
