-- Idempotent apply script for migration 20261022100000_RemovePartyInfoStateCodeForeignKey.
--
-- Drops FK_PartyInfos_StateCodes_StateCode so a company (PartyInfo) whose Country is not Malaysia can
-- hold a free-text State, same as buyers since 20260827020000_RemovePublicCustomerStateCodeForeignKey.
-- Malaysian companies (CountryCode = 'MYS') must still use a valid StateCodes row, enforced in
-- Pages/Suppliers/Create.cshtml.cs and Edit.cshtml.cs. Narrows the column from nvarchar(450) to
-- nvarchar(100) - all existing values are 2-character codes, safe.
--
-- Purely additive/non-destructive: no data is deleted, only a referential-integrity constraint and
-- an over-wide column type are relaxed.
--
-- Safe to re-run.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON; -- any error rolls back the whole transaction, so no half-applied schema gets a history row
GO

BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PartyInfos_StateCodes_StateCode' AND parent_object_id = OBJECT_ID('PartyInfos'))
BEGIN
    ALTER TABLE [PartyInfos] DROP CONSTRAINT [FK_PartyInfos_StateCodes_StateCode];
END

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PartyInfos_StateCode' AND object_id = OBJECT_ID('PartyInfos'))
BEGIN
    DROP INDEX [IX_PartyInfos_StateCode] ON [PartyInfos];
END

IF EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID('PartyInfos') AND c.name = 'StateCode' AND t.name = 'nvarchar' AND c.max_length <> 200
)
BEGIN
    ALTER TABLE [PartyInfos] ALTER COLUMN [StateCode] nvarchar(100) NOT NULL;
END

-- ── History row ──────────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261022100000_RemovePartyInfoStateCodeForeignKey')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022100000_RemovePartyInfoStateCodeForeignKey', N'10.0.13');
END;

COMMIT;
GO
