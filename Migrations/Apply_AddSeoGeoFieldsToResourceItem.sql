-- Idempotent apply script for migration 20260807120000_AddSeoGeoFieldsToResourceItem.
--
-- Adds SEO / GEO (AI-answer-engine) metadata columns to the Resources table (WebsiteDbContext),
-- powering the Manage Resources CMS readiness score. All columns are nullable or defaulted so this
-- is purely additive — existing rows remain valid with no backfill required.
--
-- NOTE: Resources/ResourceTypes belong to WebsiteDbContext, which has its OWN __EFMigrationsHistory
-- state distinct from ApplicationDbContext's. Do not run this against the wrong migrations history.
--
-- Safe to re-run.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;

IF COL_LENGTH('Resources', 'MetaTitle') IS NULL
BEGIN
    ALTER TABLE [Resources] ADD [MetaTitle] nvarchar(60) NULL;
END

IF COL_LENGTH('Resources', 'MetaDescription') IS NULL
BEGIN
    ALTER TABLE [Resources] ADD [MetaDescription] nvarchar(160) NULL;
END

IF COL_LENGTH('Resources', 'FocusKeyword') IS NULL
BEGIN
    ALTER TABLE [Resources] ADD [FocusKeyword] nvarchar(100) NULL;
END

IF COL_LENGTH('Resources', 'CanonicalUrl') IS NULL
BEGIN
    ALTER TABLE [Resources] ADD [CanonicalUrl] nvarchar(500) NULL;
END

IF COL_LENGTH('Resources', 'OgText') IS NULL
BEGIN
    ALTER TABLE [Resources] ADD [OgText] nvarchar(200) NULL;
END

IF COL_LENGTH('Resources', 'ImageAlt') IS NULL
BEGIN
    ALTER TABLE [Resources] ADD [ImageAlt] nvarchar(200) NULL;
END

IF COL_LENGTH('Resources', 'Author') IS NULL
BEGIN
    ALTER TABLE [Resources] ADD [Author] nvarchar(100) NULL;
END

IF COL_LENGTH('Resources', 'Tldr') IS NULL
BEGIN
    ALTER TABLE [Resources] ADD [Tldr] nvarchar(400) NULL;
END

IF COL_LENGTH('Resources', 'SchemaType') IS NULL
BEGIN
    ALTER TABLE [Resources] ADD [SchemaType] int NOT NULL CONSTRAINT [DF_Resources_SchemaType] DEFAULT 0;
END

IF COL_LENGTH('Resources', 'FaqItemsJson') IS NULL
BEGIN
    ALTER TABLE [Resources] ADD [FaqItemsJson] nvarchar(max) NULL;
END

-- ── History row ──────────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260807120000_AddSeoGeoFieldsToResourceItem')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260807120000_AddSeoGeoFieldsToResourceItem', N'10.0.10');
END;

COMMIT;
GO
