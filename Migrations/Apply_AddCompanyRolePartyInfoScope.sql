-- Idempotent apply script for migration 20260729092236_AddCompanyRolePartyInfoScope.
--
-- Adds a nullable PartyInfoId to CompanyRoles so a company's Owner/Admin can create custom roles
-- scoped to just their own company, alongside the existing shared system roles (Owner/Admin/Editor/
-- Viewer, which stay NULL = visible everywhere). Existing rows are untouched by this migration.
--
-- Safe to re-run.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;

IF COL_LENGTH('CompanyRoles', 'PartyInfoId') IS NULL
BEGIN
    ALTER TABLE [CompanyRoles] ADD [PartyInfoId] int NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CompanyRoles_PartyInfoId' AND object_id = OBJECT_ID('CompanyRoles'))
BEGIN
    CREATE INDEX [IX_CompanyRoles_PartyInfoId] ON [CompanyRoles] ([PartyInfoId]);
END

-- Restrict, not Cascade: PartyInfo already cascades to UserCompany, which also references CompanyRole
-- — a second cascade path from PartyInfo to CompanyRole is rejected by SQL Server ("may cause cycles
-- or multiple cascade paths"). The app deletes a company's custom roles explicitly before the company
-- itself (Pages/Suppliers/Index.cshtml.cs).
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CompanyRoles_PartyInfos_PartyInfoId')
BEGIN
    ALTER TABLE [CompanyRoles] ADD CONSTRAINT [FK_CompanyRoles_PartyInfos_PartyInfoId]
        FOREIGN KEY ([PartyInfoId]) REFERENCES [PartyInfos] ([PartyInfoId]) ON DELETE NO ACTION;
END

-- ── History row ──────────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260729092236_AddCompanyRolePartyInfoScope')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260729092236_AddCompanyRolePartyInfoScope', N'10.0.9');
END;

COMMIT;
GO
