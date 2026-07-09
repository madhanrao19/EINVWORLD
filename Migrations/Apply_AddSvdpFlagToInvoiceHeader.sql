-- Idempotent apply script for 20260709120000_AddSvdpFlagToInvoiceHeader
-- Adds the SVDP (Special Voluntary Disclosure Programme) flag to InvoiceHeaders.
-- Additive only; existing rows default to 0. Safe to run repeatedly.

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260709120000_AddSvdpFlagToInvoiceHeader')
BEGIN
    IF COL_LENGTH('InvoiceHeaders', 'IsSvdp') IS NULL
    BEGIN
        ALTER TABLE [InvoiceHeaders] ADD [IsSvdp] bit NOT NULL CONSTRAINT [DF_InvoiceHeaders_IsSvdp] DEFAULT(0);
    END

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260709120000_AddSvdpFlagToInvoiceHeader', N'10.0.9');
END
GO
