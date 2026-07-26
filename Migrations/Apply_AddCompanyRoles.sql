BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726070608_AddCompanyRoles'
)
BEGIN
    ALTER TABLE [UserCompanies] ADD [CompanyRoleId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726070608_AddCompanyRoles'
)
BEGIN
    CREATE TABLE [CompanyRoles] (
        [CompanyRoleId] int NOT NULL IDENTITY,
        [Name] nvarchar(50) NOT NULL,
        [CanManageUsers] bit NOT NULL,
        [CanEditProfile] bit NOT NULL,
        [CanManageBranding] bit NOT NULL,
        [CanViewAudit] bit NOT NULL,
        [IsSystemDefined] bit NOT NULL,
        CONSTRAINT [PK_CompanyRoles] PRIMARY KEY ([CompanyRoleId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726070608_AddCompanyRoles'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'CompanyRoleId', N'CanEditProfile', N'CanManageBranding', N'CanManageUsers', N'CanViewAudit', N'IsSystemDefined', N'Name') AND [object_id] = OBJECT_ID(N'[CompanyRoles]'))
        SET IDENTITY_INSERT [CompanyRoles] ON;
    EXEC(N'INSERT INTO [CompanyRoles] ([CompanyRoleId], [CanEditProfile], [CanManageBranding], [CanManageUsers], [CanViewAudit], [IsSystemDefined], [Name])
    VALUES (1, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), N''Owner''),
    (2, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), N''Admin''),
    (3, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), N''Editor''),
    (4, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), N''Viewer'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'CompanyRoleId', N'CanEditProfile', N'CanManageBranding', N'CanManageUsers', N'CanViewAudit', N'IsSystemDefined', N'Name') AND [object_id] = OBJECT_ID(N'[CompanyRoles]'))
        SET IDENTITY_INSERT [CompanyRoles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726070608_AddCompanyRoles'
)
BEGIN
    CREATE INDEX [IX_UserCompanies_CompanyRoleId] ON [UserCompanies] ([CompanyRoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726070608_AddCompanyRoles'
)
BEGIN
    ALTER TABLE [UserCompanies] ADD CONSTRAINT [FK_UserCompanies_CompanyRoles_CompanyRoleId] FOREIGN KEY ([CompanyRoleId]) REFERENCES [CompanyRoles] ([CompanyRoleId]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726070608_AddCompanyRoles'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260726070608_AddCompanyRoles', N'10.0.9');
END;

COMMIT;
GO

