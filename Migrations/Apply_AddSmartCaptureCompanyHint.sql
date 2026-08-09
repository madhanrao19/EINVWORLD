-- Idempotent apply script for migration 20260809010514_AddSmartCaptureCompanyHint.
--
-- Creates SmartCaptureCompanyHints: Smart Capture Stage 2 (reduced first cut). One row per company,
-- holding a streaming Boyer-Moore majority-vote of the doc type/currency/tax fields the company has
-- actually confirmed on past Smart Capture drafts (see Services/SmartCapture/SmartCaptureCompanyHintService.cs).
-- Advisory-only context fed into the AI suggestion prompt — never read by, or written from, the
-- InvoiceHeader/draft/LHDN submission path. Purely additive — no existing table is altered.
--
-- Safe to re-run.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;

IF OBJECT_ID('SmartCaptureCompanyHints', 'U') IS NULL
BEGIN
    CREATE TABLE [SmartCaptureCompanyHints] (
        [Id] int NOT NULL IDENTITY(1,1),
        [CompanyPartyInfoId] int NOT NULL,
        [MostCommonDocTypeCode] nvarchar(10) NULL,
        [DocTypeVotes] int NOT NULL,
        [MostCommonCurrency] nvarchar(10) NULL,
        [CurrencyVotes] int NOT NULL,
        [MostCommonTaxType] nvarchar(20) NULL,
        [TaxTypeVotes] int NOT NULL,
        [MostCommonTaxRatePercent] decimal(18,2) NULL,
        [TaxRateVotes] int NOT NULL,
        [SampleCount] int NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_SmartCaptureCompanyHints] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SmartCaptureCompanyHints_PartyInfos_CompanyPartyInfoId] FOREIGN KEY ([CompanyPartyInfoId])
            REFERENCES [PartyInfos] ([PartyInfoId]) ON DELETE CASCADE
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SmartCaptureCompanyHints_CompanyPartyInfoId' AND object_id = OBJECT_ID('SmartCaptureCompanyHints'))
BEGIN
    CREATE UNIQUE INDEX [IX_SmartCaptureCompanyHints_CompanyPartyInfoId] ON [SmartCaptureCompanyHints] ([CompanyPartyInfoId]);
END

-- ── History row ──────────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260809010514_AddSmartCaptureCompanyHint')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260809010514_AddSmartCaptureCompanyHint', N'10.0.10');
END;

COMMIT;
GO
