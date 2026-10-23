-- Idempotent apply script for migration 20261019092452_AddSupplierBankAccounts.
--
-- Lets a company (supplier) keep several bank accounts and pick a default:
--   - New table SupplierBankAccounts (BankAccountNo is encrypted by the app, hence nvarchar(max)),
--     FK to PartyInfos with ON DELETE CASCADE, index on PartyInfoId.
--   - Each company's existing PartyInfos.BankName / BankAccountNo is copied in as its default account.
--     The value is copied exactly as stored (ciphertext, or legacy plaintext the PII backfill encrypts).
--
-- Additive only: PartyInfos is not changed (it keeps a mirror of the default account). Safe to re-run.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON; -- any error rolls back the whole transaction, so no half-applied schema gets a history row
GO

BEGIN TRANSACTION;

IF OBJECT_ID(N'[SupplierBankAccounts]', N'U') IS NULL
BEGIN
    CREATE TABLE [SupplierBankAccounts] (
        [Id] int NOT NULL IDENTITY(1, 1),
        [PartyInfoId] int NOT NULL,
        [BankName] nvarchar(100) NOT NULL,
        [BankAccountNo] nvarchar(max) NOT NULL,
        [IsDefault] bit NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(256) NULL,
        [UpdatedDate] datetime2 NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_SupplierBankAccounts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SupplierBankAccounts_PartyInfos_PartyInfoId] FOREIGN KEY ([PartyInfoId])
            REFERENCES [PartyInfos] ([PartyInfoId]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SupplierBankAccounts_PartyInfoId' AND [object_id] = OBJECT_ID(N'[SupplierBankAccounts]'))
BEGIN
    CREATE INDEX [IX_SupplierBankAccounts_PartyInfoId] ON [SupplierBankAccounts] ([PartyInfoId]);
END
GO

-- ── Seed: existing company bank details → default account (only for companies with none yet) ─────
INSERT INTO [SupplierBankAccounts] ([PartyInfoId], [BankName], [BankAccountNo], [IsDefault], [CreatedDate], [CreatedBy])
SELECT p.[PartyInfoId], LEFT(ISNULL(LTRIM(RTRIM(p.[BankName])), N''), 100), p.[BankAccountNo], 1, GETDATE(), N'migration'
FROM [PartyInfos] p
WHERE p.[BankAccountNo] IS NOT NULL AND LTRIM(RTRIM(p.[BankAccountNo])) <> N''
  AND NOT EXISTS (SELECT 1 FROM [SupplierBankAccounts] a WHERE a.[PartyInfoId] = p.[PartyInfoId]);

-- ── History row ──────────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261019092452_AddSupplierBankAccounts')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019092452_AddSupplierBankAccounts', N'10.0.13');
END;

COMMIT;
GO
