-- Idempotent apply script for migration 20260827020000_RemovePublicCustomerStateCodeForeignKey.
--
-- Drops FK_PublicCustomers_StateCodes_StateCode so a Buyer (PublicCustomer) whose Country is not
-- Malaysia can hold a free-text State value that doesn't exist in the StateCodes table. Malaysian
-- buyers (CountryCode = 'MYS') are still restricted to a valid StateCodes row, enforced at the
-- application layer instead of the database layer (Pages/PublicCustomer/Create.cshtml.cs and
-- Edit.cshtml.cs). LHDN's own MyInvois Portal accepts free text for a foreign party's state, and
-- there is no LHDN requirement restricting CountrySubentityCode to a fixed code list — this FK was
-- a self-imposed EINVWORLD restriction, not an LHDN one. Narrows the column from nvarchar(450) (the
-- FK-shadow-property default) to nvarchar(100) — all existing values are 2-character codes, safe.
--
-- Purely additive/non-destructive: no data is deleted, only a referential-integrity constraint and
-- an over-wide column type are relaxed.
--
-- Safe to re-run.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PublicCustomers_StateCodes_StateCode' AND parent_object_id = OBJECT_ID('PublicCustomers'))
BEGIN
    ALTER TABLE [PublicCustomers] DROP CONSTRAINT [FK_PublicCustomers_StateCodes_StateCode];
END

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PublicCustomers_StateCode' AND object_id = OBJECT_ID('PublicCustomers'))
BEGIN
    DROP INDEX [IX_PublicCustomers_StateCode] ON [PublicCustomers];
END

IF EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID('PublicCustomers') AND c.name = 'StateCode' AND t.name = 'nvarchar' AND c.max_length <> 200
)
BEGIN
    ALTER TABLE [PublicCustomers] ALTER COLUMN [StateCode] nvarchar(100) NOT NULL;
END

-- ── History row ──────────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260827020000_RemovePublicCustomerStateCodeForeignKey')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260827020000_RemovePublicCustomerStateCodeForeignKey', N'10.0.10');
END;

COMMIT;
GO
