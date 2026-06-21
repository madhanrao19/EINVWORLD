-- Idempotent apply script for migration 20260621020000_AddSubmissionRecords.
-- Creates the SubmissionRecords table used for local duplicate-submission protection. Safe to run more than once.
BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621020000_AddSubmissionRecords'
)
BEGIN
    IF OBJECT_ID(N'[SubmissionRecords]', N'U') IS NULL
    BEGIN
        CREATE TABLE [SubmissionRecords] (
            [Id] int NOT NULL IDENTITY,
            [Tin] nvarchar(50) NULL,
            [PayloadHash] nvarchar(64) NOT NULL,
            [DocumentCount] int NOT NULL,
            [SubmittedAtUtc] datetime2 NOT NULL,
            [ResponseContent] nvarchar(max) NULL,
            CONSTRAINT [PK_SubmissionRecords] PRIMARY KEY ([Id])
        );

        CREATE INDEX [IX_SubmissionRecords_PayloadHash_SubmittedAtUtc]
            ON [SubmissionRecords] ([PayloadHash], [SubmittedAtUtc]);
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621020000_AddSubmissionRecords'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260621020000_AddSubmissionRecords', N'10.0.9');
END;

COMMIT;
GO
