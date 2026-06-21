-- Idempotent apply script for migration 20260621010000_AddSyncJobDurability.
-- Adds the durable-queue columns + poll index to SyncJobs so background sync/import jobs
-- survive an IIS app-pool recycle / server reboot. Safe to run more than once.
-- Run AFTER Apply_AddSyncJobTable.sql (it requires the SyncJobs table to exist).
BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621010000_AddSyncJobDurability'
)
BEGIN
    IF COL_LENGTH(N'[SyncJobs]', N'AttemptCount') IS NULL
        ALTER TABLE [SyncJobs] ADD [AttemptCount] int NOT NULL CONSTRAINT [DF_SyncJobs_AttemptCount] DEFAULT 0;

    IF COL_LENGTH(N'[SyncJobs]', N'MaxAttempts') IS NULL
        ALTER TABLE [SyncJobs] ADD [MaxAttempts] int NOT NULL CONSTRAINT [DF_SyncJobs_MaxAttempts] DEFAULT 3;

    IF COL_LENGTH(N'[SyncJobs]', N'NextRunAtUtc') IS NULL
        ALTER TABLE [SyncJobs] ADD [NextRunAtUtc] datetime2 NULL;

    IF COL_LENGTH(N'[SyncJobs]', N'LockedBy') IS NULL
        ALTER TABLE [SyncJobs] ADD [LockedBy] nvarchar(100) NULL;

    IF COL_LENGTH(N'[SyncJobs]', N'LockedUntilUtc') IS NULL
        ALTER TABLE [SyncJobs] ADD [LockedUntilUtc] datetime2 NULL;

    IF COL_LENGTH(N'[SyncJobs]', N'PayloadJson') IS NULL
        ALTER TABLE [SyncJobs] ADD [PayloadJson] nvarchar(max) NULL;

    IF NOT EXISTS (
        SELECT * FROM sys.indexes
        WHERE name = N'IX_SyncJobs_Status_NextRunAtUtc' AND object_id = OBJECT_ID(N'[SyncJobs]')
    )
        CREATE INDEX [IX_SyncJobs_Status_NextRunAtUtc] ON [SyncJobs] ([Status], [NextRunAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621010000_AddSyncJobDurability'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260621010000_AddSyncJobDurability', N'10.0.9');
END;

COMMIT;
GO
