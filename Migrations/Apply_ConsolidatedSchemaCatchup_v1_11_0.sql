-- Idempotent apply script for migration 20260726135229_ConsolidatedSchemaCatchup_v1_11_0.
--
-- This is a SQUASH of the 22 migrations from 20260423062000_AddLhdnIntermediaryRejectedFlag through
-- 20260726085937_AddUnitAndPriceToItemDescription (v1.11.0). Those 22 individual migration files and
-- their Apply_*.sql scripts were deleted from the repo and replaced by this single one. It exists
-- because a production backup audit found the live database was ~3.5 months behind head, and because
-- Staging (which auto-migrates by default) may have ALREADY applied some or all of those 22 migrations
-- individually, under their original MigrationId values, before they were squashed here.
--
-- Every operation below is existence-guarded (COL_LENGTH / OBJECT_ID / sys.indexes / sys.tables), so
-- this script is safe to run against THREE different starting states:
--   1. A database still on 20260415075935_RemovePreFix or earlier (e.g. Production) — everything below
--      is missing, so every guard fires and the full schema is built.
--   2. A database that already applied all 22 original migrations individually (e.g. an
--      already-redeployed Staging) — every guard is already satisfied, so this is a pure no-op except
--      for inserting this migration's own history row.
--   3. A database partway between the two (some but not all of the 22 applied) — each guard is
--      independent, so only the genuinely-missing pieces are created.
--
-- Data preservation notes:
--   * SystemLogs is NOT dropped. It's owned by the Serilog MSSqlServer sink (autoCreateSqlTable=true),
--     not EF — this script only ensures EF's history reflects that it no longer tracks the table.
--   * EncryptPiiFields-equivalent column widening below (BankAccountNo/Addr2/Addr3 -> nvarchar(max))
--     does NOT encrypt existing data. After this script runs, go to Admin -> System Health -> Encrypt
--     PII and run the backfill once per environment (idempotent, see PiiEncryptionBackfillService).
--   * CompanyRoles seed data (Owner/Admin/Editor/Viewer, ids 1-4) is only inserted if the table is
--     empty, so it won't collide with rows a partially-applied environment already seeded.
--
-- Safe to re-run.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;

-- ── PartyInfos: new columns ─────────────────────────────────────────────────────────────────────
IF COL_LENGTH('PartyInfos', 'LhdnIntermediaryRejected') IS NULL
BEGIN
    ALTER TABLE [PartyInfos] ADD [LhdnIntermediaryRejected] bit NOT NULL CONSTRAINT [DF_PartyInfos_LhdnIntermediaryRejected] DEFAULT(0);
END

IF COL_LENGTH('PartyInfos', 'InvoiceAccentColorHex') IS NULL
BEGIN
    ALTER TABLE [PartyInfos] ADD [InvoiceAccentColorHex] nvarchar(7) NULL;
END

IF COL_LENGTH('PartyInfos', 'InvoiceFooterNote') IS NULL
BEGIN
    ALTER TABLE [PartyInfos] ADD [InvoiceFooterNote] nvarchar(500) NULL;
END

IF COL_LENGTH('PartyInfos', 'InvoiceShowBankDetails') IS NULL
BEGIN
    ALTER TABLE [PartyInfos] ADD [InvoiceShowBankDetails] bit NOT NULL CONSTRAINT [DF_PartyInfos_InvoiceShowBankDetails] DEFAULT(1);
END

-- ── PII column widening (schema only — see note above re: the encryption backfill) ────────────────
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PartyInfos') AND name = 'BankAccountNo' AND max_length <> -1)
BEGIN
    ALTER TABLE [PartyInfos] ALTER COLUMN [BankAccountNo] nvarchar(max) NULL;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PartyInfos') AND name = 'Addr2' AND max_length <> -1)
BEGIN
    ALTER TABLE [PartyInfos] ALTER COLUMN [Addr2] nvarchar(max) NULL;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PartyInfos') AND name = 'Addr3' AND max_length <> -1)
BEGIN
    ALTER TABLE [PartyInfos] ALTER COLUMN [Addr3] nvarchar(max) NULL;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PublicCustomers') AND name = 'BankAccountNo' AND max_length <> -1)
BEGIN
    ALTER TABLE [PublicCustomers] ALTER COLUMN [BankAccountNo] nvarchar(max) NULL;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PublicCustomers') AND name = 'Addr2' AND max_length <> -1)
BEGIN
    ALTER TABLE [PublicCustomers] ALTER COLUMN [Addr2] nvarchar(max) NULL;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PublicCustomers') AND name = 'Addr3' AND max_length <> -1)
BEGIN
    ALTER TABLE [PublicCustomers] ALTER COLUMN [Addr3] nvarchar(max) NULL;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('InvoiceHeaders') AND name = 'BankAccountNo' AND max_length <> -1)
BEGIN
    ALTER TABLE [InvoiceHeaders] ALTER COLUMN [BankAccountNo] nvarchar(max) NULL;
END

-- ── ItemDescriptions: Unit + Unit Price ─────────────────────────────────────────────────────────
IF COL_LENGTH('ItemDescriptions', 'UnitCode') IS NULL
BEGIN
    ALTER TABLE [ItemDescriptions] ADD [UnitCode] nvarchar(max) NULL;
END
IF COL_LENGTH('ItemDescriptions', 'UnitPrice') IS NULL
BEGIN
    ALTER TABLE [ItemDescriptions] ADD [UnitPrice] decimal(18,4) NULL;
END

-- ── InvoiceLines / InvoiceHeaders decimal precision (rate/quantity/unit-price need more scale than
--    2dp money totals — widening only, no truncation) ──────────────────────────────────────────────
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('InvoiceLines') AND name = 'UnitPrice' AND scale = 2)
BEGIN
    ALTER TABLE [InvoiceLines] ALTER COLUMN [UnitPrice] decimal(18,4) NULL;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('InvoiceLines') AND name = 'Quantity' AND scale = 2)
BEGIN
    ALTER TABLE [InvoiceLines] ALTER COLUMN [Quantity] decimal(18,6) NULL;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('InvoiceHeaders') AND name = 'ExchangeRate' AND scale = 2)
BEGIN
    ALTER TABLE [InvoiceHeaders] ALTER COLUMN [ExchangeRate] decimal(18,6) NULL;
END

-- ── InvoiceHeaders: other new columns ───────────────────────────────────────────────────────────
IF COL_LENGTH('InvoiceHeaders', 'IsSvdp') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [IsSvdp] bit NOT NULL CONSTRAINT [DF_InvoiceHeaders_IsSvdp] DEFAULT(0);
END
IF COL_LENGTH('InvoiceHeaders', 'RowVersion') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [RowVersion] rowversion NULL;
END
IF COL_LENGTH('InvoiceHeaders', 'SubmissionClaimedAtUtc') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [SubmissionClaimedAtUtc] datetime2 NULL;
END
IF COL_LENGTH('InvoiceHeaders', 'WebhookNotifiedStatus') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [WebhookNotifiedStatus] nvarchar(20) NULL;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('InvoiceHeaders') AND name = 'RefDocumentNo' AND max_length <> 400)
BEGIN
    ALTER TABLE [InvoiceHeaders] ALTER COLUMN [RefDocumentNo] nvarchar(200) NULL;
END
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('InvoiceHeaders') AND name = 'InvoiceDirection' AND max_length <> 100)
BEGIN
    ALTER TABLE [InvoiceHeaders] ALTER COLUMN [InvoiceDirection] nvarchar(50) NULL;
END

-- ── UserCompanies: company role assignment ──────────────────────────────────────────────────────
IF COL_LENGTH('UserCompanies', 'CompanyRoleId') IS NULL
BEGIN
    ALTER TABLE [UserCompanies] ADD [CompanyRoleId] int NULL;
END

-- ── New tables ───────────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('CompanyRoles', 'U') IS NULL
BEGIN
    CREATE TABLE [CompanyRoles] (
        [CompanyRoleId] int NOT NULL IDENTITY(1,1),
        [Name] nvarchar(50) NOT NULL,
        [CanManageUsers] bit NOT NULL,
        [CanEditProfile] bit NOT NULL,
        [CanManageBranding] bit NOT NULL,
        [CanViewAudit] bit NOT NULL,
        [IsSystemDefined] bit NOT NULL,
        CONSTRAINT [PK_CompanyRoles] PRIMARY KEY ([CompanyRoleId])
    );
END

IF OBJECT_ID('CompanyInvitations', 'U') IS NULL
BEGIN
    CREATE TABLE [CompanyInvitations] (
        [CompanyInvitationId] int NOT NULL IDENTITY(1,1),
        [PartyInfoId] int NOT NULL,
        [Email] nvarchar(320) NOT NULL,
        [InvitedByUserId] nvarchar(max) NOT NULL,
        [CompanyRoleId] int NULL,
        [TokenHash] nvarchar(64) NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [AcceptedAt] datetime2 NULL,
        [RevokedAt] datetime2 NULL,
        [CreatedDate] datetime2 NOT NULL,
        CONSTRAINT [PK_CompanyInvitations] PRIMARY KEY ([CompanyInvitationId]),
        CONSTRAINT [FK_CompanyInvitations_CompanyRoles_CompanyRoleId] FOREIGN KEY ([CompanyRoleId]) REFERENCES [CompanyRoles] ([CompanyRoleId]) ON DELETE SET NULL,
        CONSTRAINT [FK_CompanyInvitations_PartyInfos_PartyInfoId] FOREIGN KEY ([PartyInfoId]) REFERENCES [PartyInfos] ([PartyInfoId]) ON DELETE CASCADE
    );
END

IF OBJECT_ID('AuditLogs', 'U') IS NULL
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] bigint NOT NULL IDENTITY(1,1),
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
END

IF OBJECT_ID('SavedInvoiceViews', 'U') IS NULL
BEGIN
    CREATE TABLE [SavedInvoiceViews] (
        [Id] int NOT NULL IDENTITY(1,1),
        [UserId] nvarchar(450) NOT NULL,
        [InvoiceDirection] nvarchar(20) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [QueryString] nvarchar(2048) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_SavedInvoiceViews] PRIMARY KEY ([Id])
    );
END

IF OBJECT_ID('SubmissionRecords', 'U') IS NULL
BEGIN
    CREATE TABLE [SubmissionRecords] (
        [Id] int NOT NULL IDENTITY(1,1),
        [Tin] nvarchar(50) NULL,
        [PayloadHash] nvarchar(64) NOT NULL,
        [DocumentCount] int NOT NULL,
        [SubmittedAtUtc] datetime2 NOT NULL,
        [ResponseContent] nvarchar(max) NULL,
        CONSTRAINT [PK_SubmissionRecords] PRIMARY KEY ([Id])
    );
END

IF OBJECT_ID('SyncJobs', 'U') IS NULL
BEGIN
    CREATE TABLE [SyncJobs] (
        [Id] int NOT NULL IDENTITY(1,1),
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
        [AttemptCount] int NOT NULL,
        [MaxAttempts] int NOT NULL,
        [NextRunAtUtc] datetime2 NULL,
        [LockedBy] nvarchar(100) NULL,
        [LockedUntilUtc] datetime2 NULL,
        [PayloadJson] nvarchar(max) NULL,
        CONSTRAINT [PK_SyncJobs] PRIMARY KEY ([Id])
    );
END

IF OBJECT_ID('WebhookSubscriptions', 'U') IS NULL
BEGIN
    CREATE TABLE [WebhookSubscriptions] (
        [Id] int NOT NULL IDENTITY(1,1),
        [Tin] nvarchar(50) NOT NULL,
        [CallbackUrl] nvarchar(2048) NOT NULL,
        [Secret] nvarchar(max) NOT NULL,
        [Description] nvarchar(200) NULL,
        [IsEnabled] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(256) NULL,
        [LastDeliveryAtUtc] datetime2 NULL,
        [LastDeliveryResult] nvarchar(500) NULL,
        CONSTRAINT [PK_WebhookSubscriptions] PRIMARY KEY ([Id])
    );
END

-- ── CompanyRoles seed data (Owner/Admin/Editor/Viewer) ──────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [CompanyRoles])
BEGIN
    SET IDENTITY_INSERT [CompanyRoles] ON;
    INSERT INTO [CompanyRoles] ([CompanyRoleId], [Name], [CanManageUsers], [CanEditProfile], [CanManageBranding], [CanViewAudit], [IsSystemDefined])
    VALUES
        (1, N'Owner',  1, 1, 1, 1, 1),
        (2, N'Admin',  1, 1, 1, 1, 1),
        (3, N'Editor', 0, 1, 0, 0, 1),
        (4, N'Viewer', 0, 0, 0, 0, 1);
    SET IDENTITY_INSERT [CompanyRoles] OFF;
END

-- ── Foreign key: UserCompanies -> CompanyRoles ──────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_UserCompanies_CompanyRoles_CompanyRoleId')
BEGIN
    ALTER TABLE [UserCompanies] ADD CONSTRAINT [FK_UserCompanies_CompanyRoles_CompanyRoleId]
        FOREIGN KEY ([CompanyRoleId]) REFERENCES [CompanyRoles] ([CompanyRoleId]) ON DELETE SET NULL;
END

-- ── Indexes ──────────────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserCompanies_CompanyRoleId' AND object_id = OBJECT_ID('UserCompanies'))
BEGIN
    CREATE INDEX [IX_UserCompanies_CompanyRoleId] ON [UserCompanies] ([CompanyRoleId]);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InvoiceHistories_InvoiceNo' AND object_id = OBJECT_ID('InvoiceHistories'))
BEGIN
    CREATE INDEX [IX_InvoiceHistories_InvoiceNo] ON [InvoiceHistories] ([InvoiceNo]);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InvoiceHeaders_CreatedDate' AND object_id = OBJECT_ID('InvoiceHeaders'))
BEGIN
    CREATE INDEX [IX_InvoiceHeaders_CreatedDate] ON [InvoiceHeaders] ([CreatedDate]);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InvoiceHeaders_InvoiceDirection' AND object_id = OBJECT_ID('InvoiceHeaders'))
BEGIN
    CREATE INDEX [IX_InvoiceHeaders_InvoiceDirection] ON [InvoiceHeaders] ([InvoiceDirection]);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InvoiceHeaders_LHDNStatusId_LastUpdated' AND object_id = OBJECT_ID('InvoiceHeaders'))
BEGIN
    CREATE INDEX [IX_InvoiceHeaders_LHDNStatusId_LastUpdated] ON [InvoiceHeaders] ([LHDNStatusId], [LastUpdated]);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InvoiceHeaders_RefDocumentNo' AND object_id = OBJECT_ID('InvoiceHeaders'))
BEGIN
    CREATE INDEX [IX_InvoiceHeaders_RefDocumentNo] ON [InvoiceHeaders] ([RefDocumentNo]);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InvoiceHeaders_UUID' AND object_id = OBJECT_ID('InvoiceHeaders'))
BEGIN
    CREATE INDEX [IX_InvoiceHeaders_UUID] ON [InvoiceHeaders] ([UUID]);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogs_Action' AND object_id = OBJECT_ID('AuditLogs'))
BEGIN
    CREATE INDEX [IX_AuditLogs_Action] ON [AuditLogs] ([Action]);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogs_CreatedAtUtc' AND object_id = OBJECT_ID('AuditLogs'))
BEGIN
    CREATE INDEX [IX_AuditLogs_CreatedAtUtc] ON [AuditLogs] ([CreatedAtUtc]);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CompanyInvitations_CompanyRoleId' AND object_id = OBJECT_ID('CompanyInvitations'))
BEGIN
    CREATE INDEX [IX_CompanyInvitations_CompanyRoleId] ON [CompanyInvitations] ([CompanyRoleId]);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CompanyInvitations_PartyInfoId' AND object_id = OBJECT_ID('CompanyInvitations'))
BEGIN
    CREATE INDEX [IX_CompanyInvitations_PartyInfoId] ON [CompanyInvitations] ([PartyInfoId]);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CompanyInvitations_TokenHash' AND object_id = OBJECT_ID('CompanyInvitations'))
BEGIN
    CREATE INDEX [IX_CompanyInvitations_TokenHash] ON [CompanyInvitations] ([TokenHash]);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SubmissionRecords_PayloadHash_SubmittedAtUtc' AND object_id = OBJECT_ID('SubmissionRecords'))
BEGIN
    CREATE INDEX [IX_SubmissionRecords_PayloadHash_SubmittedAtUtc] ON [SubmissionRecords] ([PayloadHash], [SubmittedAtUtc]);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WebhookSubscriptions_Tin' AND object_id = OBJECT_ID('WebhookSubscriptions'))
BEGIN
    CREATE INDEX [IX_WebhookSubscriptions_Tin] ON [WebhookSubscriptions] ([Tin]);
END

-- Superseded single-column index: replaced by IX_InvoiceHeaders_LHDNStatusId_LastUpdated above.
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InvoiceHeaders_LHDNStatusId' AND object_id = OBJECT_ID('InvoiceHeaders'))
BEGIN
    DROP INDEX [IX_InvoiceHeaders_LHDNStatusId] ON [InvoiceHeaders];
END
-- Unused, dropped by the original FixPendingModelChanges migration (folded into this squash).
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SyncJobs_Status_NextRunAtUtc' AND object_id = OBJECT_ID('SyncJobs'))
BEGIN
    DROP INDEX [IX_SyncJobs_Status_NextRunAtUtc] ON [SyncJobs];
END

-- ── History row ──────────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260726135229_ConsolidatedSchemaCatchup_v1_11_0')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260726135229_ConsolidatedSchemaCatchup_v1_11_0', N'10.0.9');
END;

COMMIT;
GO
