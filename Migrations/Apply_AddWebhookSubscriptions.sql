-- Idempotent apply script for migration 20260704000000_AddWebhookSubscriptions.
-- Adds the WebhookSubscriptions table and the InvoiceHeaders.WebhookNotifiedStatus dedup column.
-- Purely additive; safe to run more than once.
BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704000000_AddWebhookSubscriptions'
)
BEGIN
    IF NOT EXISTS (
        SELECT * FROM sys.columns
        WHERE name = N'WebhookNotifiedStatus' AND object_id = OBJECT_ID(N'[InvoiceHeaders]')
    )
    BEGIN
        ALTER TABLE [InvoiceHeaders] ADD [WebhookNotifiedStatus] nvarchar(20) NULL;
    END;

    IF OBJECT_ID(N'[WebhookSubscriptions]', N'U') IS NULL
    BEGIN
        CREATE TABLE [WebhookSubscriptions] (
            [Id] int NOT NULL IDENTITY,
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

        CREATE INDEX [IX_WebhookSubscriptions_Tin] ON [WebhookSubscriptions] ([Tin]);
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704000000_AddWebhookSubscriptions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260704000000_AddWebhookSubscriptions', N'10.0.9');
END;

COMMIT;
GO
