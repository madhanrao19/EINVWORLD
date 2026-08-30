-- Idempotent apply script for migration 20260809023129_AddSmartCaptureAutoSubmit.
--
-- Smart Capture Stage 4 (reduced first cut): adds SmartCaptureDocuments.PendingAutoSubmitJobId (nullable
-- FK-by-convention to SyncJobs.Id, tracked purely for display/cancel — not a real DB FK since SyncJobs is
-- a generic queue table used by many unrelated job types) and the new SmartCaptureAutoSubmitSettings
-- table (one row per company, the explicit opt-in a system Admin sets via Pages/Admin/SmartCaptureAutoSubmit
-- — never self-service). Purely additive — no existing table is altered destructively.
--
-- Safe to re-run.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SmartCaptureDocuments') AND name = 'PendingAutoSubmitJobId')
BEGIN
    ALTER TABLE [SmartCaptureDocuments] ADD [PendingAutoSubmitJobId] int NULL;
END

IF OBJECT_ID('SmartCaptureAutoSubmitSettings', 'U') IS NULL
BEGIN
    CREATE TABLE [SmartCaptureAutoSubmitSettings] (
        [Id] int NOT NULL IDENTITY(1,1),
        [CompanyPartyInfoId] int NOT NULL,
        [Enabled] bit NOT NULL,
        [AllowedDocTypesCsv] nvarchar(80) NOT NULL,
        [MaxAutoSubmitValue] decimal(18,2) NOT NULL,
        [DelayMinutes] int NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [UpdatedByUserId] nvarchar(450) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_SmartCaptureAutoSubmitSettings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SmartCaptureAutoSubmitSettings_PartyInfos_CompanyPartyInfoId] FOREIGN KEY ([CompanyPartyInfoId])
            REFERENCES [PartyInfos] ([PartyInfoId]) ON DELETE CASCADE
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SmartCaptureAutoSubmitSettings_CompanyPartyInfoId' AND object_id = OBJECT_ID('SmartCaptureAutoSubmitSettings'))
BEGIN
    CREATE UNIQUE INDEX [IX_SmartCaptureAutoSubmitSettings_CompanyPartyInfoId] ON [SmartCaptureAutoSubmitSettings] ([CompanyPartyInfoId]);
END

-- ── History row ──────────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260809023129_AddSmartCaptureAutoSubmit')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260809023129_AddSmartCaptureAutoSubmit', N'10.0.10');
END;

COMMIT;
GO
