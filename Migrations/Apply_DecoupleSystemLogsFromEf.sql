BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618174454_DecoupleSystemLogsFromEf'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260618174454_DecoupleSystemLogsFromEf', N'10.0.9');
END;

COMMIT;
GO

