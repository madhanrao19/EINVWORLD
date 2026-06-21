-- Idempotent apply script for migration 20260621030000_AddAuditLog.
-- Creates the tamper-evident, hash-chained AuditLogs table. Safe to run more than once.
BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621030000_AddAuditLog'
)
BEGIN
    IF OBJECT_ID(N'[AuditLogs]', N'U') IS NULL
    BEGIN
        CREATE TABLE [AuditLogs] (
            [Id] bigint NOT NULL IDENTITY,
            [CorrelationId] nvarchar(64) NULL,
            [CreatedAtUtc] datetime2 NOT NULL,
            [Action] nvarchar(80) NOT NULL,
            [UserId] nvarchar(450) NULL,
            [UserName] nvarchar(256) NULL,
            [Tin] nvarchar(50) NULL,
            [InvoiceNo] nvarchar(100) NULL,
            [Uuid] nvarchar(100) NULL,
            [OldValueJson] nvarchar(max) NULL,
            [NewValueJson] nvarchar(max) NULL,
            [IpAddress] nvarchar(64) NULL,
            [UserAgent] nvarchar(512) NULL,
            [PreviousHash] nvarchar(64) NOT NULL,
            [RowHash] nvarchar(64) NOT NULL,
            CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
        );

        CREATE INDEX [IX_AuditLogs_Action] ON [AuditLogs] ([Action]);
        CREATE INDEX [IX_AuditLogs_CreatedAtUtc] ON [AuditLogs] ([CreatedAtUtc]);
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621030000_AddAuditLog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260621030000_AddAuditLog', N'10.0.9');
END;

COMMIT;
GO
