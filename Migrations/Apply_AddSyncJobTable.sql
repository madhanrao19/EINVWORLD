BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618132909_AddSyncJobTable'
)
BEGIN
    CREATE TABLE [SyncJobs] (
        [Id] int NOT NULL IDENTITY,
        [Tin] nvarchar(50) NOT NULL,
        [JobType] nvarchar(40) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [QueuedAtUtc] datetime2 NOT NULL,
        [StartedAtUtc] datetime2 NULL,
        [FinishedAtUtc] datetime2 NULL,
        [ImportedCount] int NOT NULL,
        [ErrorCount] int NOT NULL,
        [Message] nvarchar(2000) NULL,
        [TriggeredBy] nvarchar(256) NULL,
        CONSTRAINT [PK_SyncJobs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618132909_AddSyncJobTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260618132909_AddSyncJobTable', N'10.0.9');
END;

COMMIT;
GO

