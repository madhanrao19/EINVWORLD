-- Idempotent apply script for migration 20260619000000_FixInvoiceDecimalPrecision.
-- Widens rate/quantity/unit-price columns beyond the blanket decimal(18,2) so FX rates
-- and fractional quantities/unit-prices are no longer truncated. All changes are widening
-- (scale increase only) and preserve existing data. Safe to run more than once.
BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619000000_FixInvoiceDecimalPrecision'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ALTER COLUMN [ExchangeRate] decimal(18,6) NULL;
    ALTER TABLE [InvoiceLines]   ALTER COLUMN [Quantity]     decimal(18,6) NULL;
    ALTER TABLE [InvoiceLines]   ALTER COLUMN [UnitPrice]    decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619000000_FixInvoiceDecimalPrecision'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260619000000_FixInvoiceDecimalPrecision', N'10.0.9');
END;

COMMIT;
GO
