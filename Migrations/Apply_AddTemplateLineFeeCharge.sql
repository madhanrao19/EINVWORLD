-- Idempotent apply script for migration 20261024120000_AddTemplateLineFeeCharge.
--
-- InvoiceTemplateLines.FeeChargeAmount / FeeChargeReason: a template line keeps its Fee / Charge, the same
-- columns InvoiceLines already has. Both are nullable and additive; no existing row is touched
-- (NULL = no fee, exactly how templates behaved before). Safe to re-run.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON; -- any error rolls back the whole transaction, so no half-applied schema gets a history row
GO

BEGIN TRANSACTION;

IF COL_LENGTH('InvoiceTemplateLines', 'FeeChargeAmount') IS NULL
BEGIN
    ALTER TABLE [InvoiceTemplateLines] ADD [FeeChargeAmount] decimal(18,2) NULL;
END

IF COL_LENGTH('InvoiceTemplateLines', 'FeeChargeReason') IS NULL
BEGIN
    ALTER TABLE [InvoiceTemplateLines] ADD [FeeChargeReason] nvarchar(200) NULL;
END

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261024120000_AddTemplateLineFeeCharge')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261024120000_AddTemplateLineFeeCharge', N'10.0.13');
END;

COMMIT;
GO
