-- Idempotent apply script for 20260709000000_AddGrossTonUnitType
-- LHDN SDK (28 Dec 2024): unit code "GT" (gross ton) added to the Unit of Measurement code table.
-- Safe to run repeatedly; additive only.

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260709000000_AddGrossTonUnitType')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [UnitTypes] WHERE [Code] = N'GT')
    BEGIN
        INSERT INTO [UnitTypes] ([Code], [Name], [IsActive], [UpdatedBy], [UpdatedDate])
        VALUES (N'GT', N'gross ton', 1, N'system', GETUTCDATE());
    END

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260709000000_AddGrossTonUnitType', N'10.0.9');
END
GO
