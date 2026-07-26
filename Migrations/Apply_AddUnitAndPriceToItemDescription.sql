BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726085937_AddUnitAndPriceToItemDescription'
)
BEGIN
    ALTER TABLE [ItemDescriptions] ADD [UnitCode] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726085937_AddUnitAndPriceToItemDescription'
)
BEGIN
    ALTER TABLE [ItemDescriptions] ADD [UnitPrice] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726085937_AddUnitAndPriceToItemDescription'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260726085937_AddUnitAndPriceToItemDescription', N'10.0.9');
END;

COMMIT;
GO

