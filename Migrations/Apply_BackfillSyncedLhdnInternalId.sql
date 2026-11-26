-- Idempotent apply script for migration 20261126230000_BackfillSyncedLhdnInternalId.
--
-- Data only, no schema change. Synced invoices whose LHDN internal ID collided with an existing InvoiceNo were stored
-- as "X(1)" in both InvoiceNo and PrefixedID; this sets PrefixedID back to LHDN's "X", so credit/debit/refund notes
-- reference the original's real ID (DR313). Only full-sync rows with PrefixedID = InvoiceNo, a "(digits)" suffix and
-- an existing "X" row are touched. Safe to re-run (a fixed row no longer has PrefixedID = InvoiceNo).

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261126230000_BackfillSyncedLhdnInternalId')
BEGIN
    UPDATE h SET PrefixedID = s.Stripped
    FROM [InvoiceHeaders] h
    CROSS APPLY (SELECT LEFT(h.InvoiceNo, LEN(h.InvoiceNo) - CHARINDEX('(', REVERSE(h.InvoiceNo))) AS Stripped,
                        CHARINDEX('(', REVERSE(h.InvoiceNo)) AS Pos) s
    WHERE h.ValidationEmailSentTo = N'System (Skipped for Sync)'
      AND h.PrefixedID = h.InvoiceNo
      AND RIGHT(h.InvoiceNo, 1) = N')'
      AND s.Pos > 2
      AND SUBSTRING(h.InvoiceNo, LEN(h.InvoiceNo) - s.Pos + 2, s.Pos - 2) NOT LIKE N'%[^0-9]%'
      AND EXISTS (SELECT 1 FROM [InvoiceHeaders] o WHERE o.InvoiceNo = s.Stripped);

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261126230000_BackfillSyncedLhdnInternalId', N'10.0.13');
END;

COMMIT;
GO
