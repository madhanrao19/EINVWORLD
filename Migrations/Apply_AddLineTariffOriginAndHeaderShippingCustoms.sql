-- Idempotent apply script for migration 20260902010000_AddLineTariffOriginAndHeaderShippingCustoms.
--
-- Adds the new schema for Phase B of the Invoice Items UX redesign:
--   - InvoiceLines: ProductTariffCode, CountryOfOrigin (FK to CountryCodes) — line-level
--     "Additional Information" fields, primarily for goods — plus DiscountReason, FeeChargeAmount,
--     FeeChargeReason, the new line-level Discount/Fee-Charge fields (DiscountAmount already existed).
--   - InvoiceHeaders: Shipping Recipient (name/address/postcode/city/state/country FK/ID type FK/
--     ID number/TIN) and Customs/Import-Export fields (Customs Form No.1/No.2 references, FTA info,
--     Certified Exporter Authorization Number, Other Charges amount/description) — invoice-level
--     "Additional Information" fields, none of which belong on a line item.
--
-- All new columns are nullable and additive; no existing row is touched. The two new lookup FKs
-- (CountryCodes, RegistrationTypes) use ON DELETE RESTRICT so a referenced lookup row can't be
-- deleted out from under an invoice that uses it.
--
-- Safe to re-run.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;

-- ── InvoiceLines ─────────────────────────────────────────────────────────────────────────────────
IF COL_LENGTH('InvoiceLines', 'ProductTariffCode') IS NULL
BEGIN
    ALTER TABLE [InvoiceLines] ADD [ProductTariffCode] nvarchar(50) NULL;
END

IF COL_LENGTH('InvoiceLines', 'CountryOfOrigin') IS NULL
BEGIN
    -- nvarchar(450), not (3): SQL Server requires an FK's two sides to share the exact same
    -- column length, and CountryCodes.Code (the referenced PK) is nvarchar(450). Every real
    -- value is still a 3-char ISO code, enforced at the application layer.
    ALTER TABLE [InvoiceLines] ADD [CountryOfOrigin] nvarchar(450) NULL;
END

IF COL_LENGTH('InvoiceLines', 'DiscountReason') IS NULL
BEGIN
    ALTER TABLE [InvoiceLines] ADD [DiscountReason] nvarchar(200) NULL;
END

IF COL_LENGTH('InvoiceLines', 'FeeChargeAmount') IS NULL
BEGIN
    ALTER TABLE [InvoiceLines] ADD [FeeChargeAmount] decimal(18,2) NULL;
END

IF COL_LENGTH('InvoiceLines', 'FeeChargeReason') IS NULL
BEGIN
    ALTER TABLE [InvoiceLines] ADD [FeeChargeReason] nvarchar(200) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InvoiceLines_CountryOfOrigin' AND object_id = OBJECT_ID('InvoiceLines'))
BEGIN
    CREATE INDEX [IX_InvoiceLines_CountryOfOrigin] ON [InvoiceLines] ([CountryOfOrigin]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_InvoiceLines_CountryCodes_CountryOfOrigin' AND parent_object_id = OBJECT_ID('InvoiceLines'))
BEGIN
    ALTER TABLE [InvoiceLines] ADD CONSTRAINT [FK_InvoiceLines_CountryCodes_CountryOfOrigin]
        FOREIGN KEY ([CountryOfOrigin]) REFERENCES [CountryCodes] ([Code]) ON DELETE NO ACTION;
END

-- ── InvoiceHeaders: Shipping Recipient ──────────────────────────────────────────────────────────
IF COL_LENGTH('InvoiceHeaders', 'ShippingRecipientName') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [ShippingRecipientName] nvarchar(200) NULL;
END

IF COL_LENGTH('InvoiceHeaders', 'ShippingRecipientAddrLine1') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [ShippingRecipientAddrLine1] nvarchar(200) NULL;
END

IF COL_LENGTH('InvoiceHeaders', 'ShippingRecipientAddrLine2') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [ShippingRecipientAddrLine2] nvarchar(200) NULL;
END

IF COL_LENGTH('InvoiceHeaders', 'ShippingRecipientAddrLine3') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [ShippingRecipientAddrLine3] nvarchar(200) NULL;
END

IF COL_LENGTH('InvoiceHeaders', 'ShippingRecipientPostcode') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [ShippingRecipientPostcode] nvarchar(20) NULL;
END

IF COL_LENGTH('InvoiceHeaders', 'ShippingRecipientCity') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [ShippingRecipientCity] nvarchar(100) NULL;
END

IF COL_LENGTH('InvoiceHeaders', 'ShippingRecipientState') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [ShippingRecipientState] nvarchar(100) NULL;
END

IF COL_LENGTH('InvoiceHeaders', 'ShippingRecipientCountryCode') IS NULL
BEGIN
    -- nvarchar(450), not (3) - see the CountryOfOrigin comment above; same FK-length
    -- requirement against CountryCodes.Code.
    ALTER TABLE [InvoiceHeaders] ADD [ShippingRecipientCountryCode] nvarchar(450) NULL;
END

IF COL_LENGTH('InvoiceHeaders', 'ShippingRecipientIdType') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [ShippingRecipientIdType] nvarchar(10) NULL;
END

IF COL_LENGTH('InvoiceHeaders', 'ShippingRecipientIdNumber') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [ShippingRecipientIdNumber] nvarchar(150) NULL;
END

IF COL_LENGTH('InvoiceHeaders', 'ShippingRecipientTIN') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [ShippingRecipientTIN] nvarchar(20) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InvoiceHeaders_ShippingRecipientCountryCode' AND object_id = OBJECT_ID('InvoiceHeaders'))
BEGIN
    CREATE INDEX [IX_InvoiceHeaders_ShippingRecipientCountryCode] ON [InvoiceHeaders] ([ShippingRecipientCountryCode]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InvoiceHeaders_ShippingRecipientIdType' AND object_id = OBJECT_ID('InvoiceHeaders'))
BEGIN
    CREATE INDEX [IX_InvoiceHeaders_ShippingRecipientIdType] ON [InvoiceHeaders] ([ShippingRecipientIdType]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_InvoiceHeaders_CountryCodes_ShippingRecipientCountryCode' AND parent_object_id = OBJECT_ID('InvoiceHeaders'))
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD CONSTRAINT [FK_InvoiceHeaders_CountryCodes_ShippingRecipientCountryCode]
        FOREIGN KEY ([ShippingRecipientCountryCode]) REFERENCES [CountryCodes] ([Code]) ON DELETE NO ACTION;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_InvoiceHeaders_RegistrationTypes_ShippingRecipientIdType' AND parent_object_id = OBJECT_ID('InvoiceHeaders'))
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD CONSTRAINT [FK_InvoiceHeaders_RegistrationTypes_ShippingRecipientIdType]
        FOREIGN KEY ([ShippingRecipientIdType]) REFERENCES [RegistrationTypes] ([Code]) ON DELETE NO ACTION;
END

-- ── InvoiceHeaders: Customs / Import-Export ─────────────────────────────────────────────────────
IF COL_LENGTH('InvoiceHeaders', 'CustomsFormNo1Reference') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [CustomsFormNo1Reference] nvarchar(500) NULL;
END

IF COL_LENGTH('InvoiceHeaders', 'FreeTradeAgreementInfo') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [FreeTradeAgreementInfo] nvarchar(200) NULL;
END

IF COL_LENGTH('InvoiceHeaders', 'CertifiedExporterAuthorizationNumber') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [CertifiedExporterAuthorizationNumber] nvarchar(100) NULL;
END

IF COL_LENGTH('InvoiceHeaders', 'CustomsFormNo2Reference') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [CustomsFormNo2Reference] nvarchar(500) NULL;
END

IF COL_LENGTH('InvoiceHeaders', 'OtherChargesAmount') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [OtherChargesAmount] decimal(18,2) NULL;
END

IF COL_LENGTH('InvoiceHeaders', 'OtherChargesDescription') IS NULL
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [OtherChargesDescription] nvarchar(500) NULL;
END

-- ── History row ──────────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260902010000_AddLineTariffOriginAndHeaderShippingCustoms')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260902010000_AddLineTariffOriginAndHeaderShippingCustoms', N'10.0.10');
END;

COMMIT;
GO
