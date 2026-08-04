-- Idempotent apply script for migration 20260804080345_AddSmartCaptureDocument.
--
-- Creates SmartCaptureDocuments: Stage 1 of the persisted/async Smart Capture pipeline (see
-- Services/SmartCapture/*). Tracks an uploaded supplier invoice document, its normalized OCR/LLM
-- extraction result (NormalizedExtractionJson — encrypted at rest via ApplicationDbContext's field-level
-- PII protector, same mechanism as InvoiceHeader.BankAccountNo), and the eventual Draft invoice it
-- produces (RelatedInvoiceHeaderInvoiceNo -> InvoiceHeaders.InvoiceNo, InvoiceHeader's actual primary
-- key). Purely additive — no existing table is altered.
--
-- Safe to re-run.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;

IF OBJECT_ID('SmartCaptureDocuments', 'U') IS NULL
BEGIN
    CREATE TABLE [SmartCaptureDocuments] (
        [Id] int NOT NULL IDENTITY(1,1),
        [CompanyPartyInfoId] int NOT NULL,
        [UploadedByUserId] nvarchar(450) NOT NULL,
        [OriginalFileName] nvarchar(260) NOT NULL,
        [InternalStorageReference] nvarchar(300) NOT NULL,
        [ContentType] nvarchar(100) NOT NULL,
        [FileSize] bigint NOT NULL,
        [FileHash] nvarchar(64) NULL,
        [PageCount] int NULL,
        [Status] nvarchar(30) NOT NULL,
        [NormalizedExtractionJson] nvarchar(max) NULL,
        [OverallConfidence] decimal(18,2) NULL,
        [UsedOcr] bit NOT NULL,
        [ConfirmedDocTypeCode] nvarchar(10) NULL,
        [RelatedInvoiceHeaderInvoiceNo] nvarchar(50) NULL,
        [FailureCode] nvarchar(60) NULL,
        [UserSafeFailureMessage] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [FileDeletedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_SmartCaptureDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SmartCaptureDocuments_PartyInfos_CompanyPartyInfoId] FOREIGN KEY ([CompanyPartyInfoId])
            REFERENCES [PartyInfos] ([PartyInfoId]) ON DELETE CASCADE
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SmartCaptureDocuments_CompanyPartyInfoId' AND object_id = OBJECT_ID('SmartCaptureDocuments'))
BEGIN
    CREATE INDEX [IX_SmartCaptureDocuments_CompanyPartyInfoId] ON [SmartCaptureDocuments] ([CompanyPartyInfoId]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SmartCaptureDocuments_FileDeletedAtUtc' AND object_id = OBJECT_ID('SmartCaptureDocuments'))
BEGIN
    CREATE INDEX [IX_SmartCaptureDocuments_FileDeletedAtUtc] ON [SmartCaptureDocuments] ([FileDeletedAtUtc]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SmartCaptureDocuments_FileHash' AND object_id = OBJECT_ID('SmartCaptureDocuments'))
BEGIN
    CREATE INDEX [IX_SmartCaptureDocuments_FileHash] ON [SmartCaptureDocuments] ([FileHash]);
END

-- ── History row ──────────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260804080345_AddSmartCaptureDocument')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260804080345_AddSmartCaptureDocument', N'10.0.10');
END;

COMMIT;
GO
