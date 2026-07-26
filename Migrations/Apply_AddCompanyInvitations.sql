BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726071747_AddCompanyInvitations'
)
BEGIN
    CREATE TABLE [CompanyInvitations] (
        [CompanyInvitationId] int NOT NULL IDENTITY,
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726071747_AddCompanyInvitations'
)
BEGIN
    CREATE INDEX [IX_CompanyInvitations_CompanyRoleId] ON [CompanyInvitations] ([CompanyRoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726071747_AddCompanyInvitations'
)
BEGIN
    CREATE INDEX [IX_CompanyInvitations_PartyInfoId] ON [CompanyInvitations] ([PartyInfoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726071747_AddCompanyInvitations'
)
BEGIN
    CREATE INDEX [IX_CompanyInvitations_TokenHash] ON [CompanyInvitations] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726071747_AddCompanyInvitations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260726071747_AddCompanyInvitations', N'10.0.9');
END;

COMMIT;
GO

