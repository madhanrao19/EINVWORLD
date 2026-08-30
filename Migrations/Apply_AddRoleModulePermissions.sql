-- Idempotent apply script for migration 20260729084820_AddRoleModulePermissions.
--
-- Adds the RoleModulePermissions table backing Admin > User Management > Role Management. A missing
-- row for a role/module means "allowed" (see ModuleAccessPageFilter), so this migration never changes
-- existing behavior on its own — it only creates the table an admin can later use to restrict access.
--
-- Safe to re-run.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;

IF OBJECT_ID('RoleModulePermissions') IS NULL
BEGIN
    CREATE TABLE [RoleModulePermissions] (
        [Id] int NOT NULL IDENTITY(1,1),
        [RoleName] nvarchar(50) NOT NULL,
        [ModuleKey] nvarchar(50) NOT NULL,
        [IsAllowed] bit NOT NULL,
        CONSTRAINT [PK_RoleModulePermissions] PRIMARY KEY ([Id])
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RoleModulePermissions_RoleName_ModuleKey' AND object_id = OBJECT_ID('RoleModulePermissions'))
BEGIN
    CREATE UNIQUE INDEX [IX_RoleModulePermissions_RoleName_ModuleKey] ON [RoleModulePermissions] ([RoleName], [ModuleKey]);
END

-- ── History row ──────────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260729084820_AddRoleModulePermissions')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260729084820_AddRoleModulePermissions', N'10.0.9');
END;

COMMIT;
GO
