BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726073135_AddInvoiceBrandingToPartyInfo'
)
BEGIN
    ALTER TABLE [PartyInfos] ADD [InvoiceAccentColorHex] nvarchar(7) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726073135_AddInvoiceBrandingToPartyInfo'
)
BEGIN
    ALTER TABLE [PartyInfos] ADD [InvoiceFooterNote] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726073135_AddInvoiceBrandingToPartyInfo'
)
BEGIN
    ALTER TABLE [PartyInfos] ADD [InvoiceShowBankDetails] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726073135_AddInvoiceBrandingToPartyInfo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260726073135_AddInvoiceBrandingToPartyInfo', N'10.0.9');
END;

COMMIT;
GO

