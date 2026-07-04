-- Idempotent apply script for migration 20260703000000_EncryptPiiFields.
-- Widens the field-level PII columns (bank account numbers + secondary/tertiary address lines) from
-- nvarchar(150) to nvarchar(max) so they can hold DataProtection ciphertext. Purely additive — no data is
-- lost or truncated. Encrypting the existing row VALUES in place is a separate, admin-triggered step
-- (Admin -> System Health -> "Encrypt existing PII"), NOT part of this script.
-- Safe to run more than once. InvoiceTemplates.BankAccountNo is already nvarchar(max) and is not altered.
BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260703000000_EncryptPiiFields'
)
BEGIN
    ALTER TABLE [InvoiceHeaders]  ALTER COLUMN [BankAccountNo] nvarchar(max) NULL;

    ALTER TABLE [PartyInfos]      ALTER COLUMN [Addr2]         nvarchar(max) NULL;
    ALTER TABLE [PartyInfos]      ALTER COLUMN [Addr3]         nvarchar(max) NULL;
    ALTER TABLE [PartyInfos]      ALTER COLUMN [BankAccountNo] nvarchar(max) NULL;

    ALTER TABLE [PublicCustomers] ALTER COLUMN [Addr2]         nvarchar(max) NULL;
    ALTER TABLE [PublicCustomers] ALTER COLUMN [Addr3]         nvarchar(max) NULL;
    ALTER TABLE [PublicCustomers] ALTER COLUMN [BankAccountNo] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260703000000_EncryptPiiFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260703000000_EncryptPiiFields', N'10.0.9');
END;

COMMIT;
GO
