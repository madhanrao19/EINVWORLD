-- ============================================================================
-- Reconcile orphaned LHDN submissions (accepted at LHDN, never persisted locally)
--
-- Background: between v1.8.2 (InvoiceHeader.RowVersion) and the submission-guard
-- reload fix, every UI submit persisted nothing locally: LHDN accepted the
-- document, but the UUID/SubmissionID/status update failed with a concurrency
-- conflict. Those invoices still look like Drafts and could be RESUBMITTED,
-- creating a real duplicate e-invoice at LHDN.
--
-- How to use (in a low-traffic window, AFTER deploying the guard fix):
--   1. Run SECTION 1 to list candidate orphans.
--   2. For each row, confirm in the MyInvois portal (or via Get Submission)
--      that the document exists at LHDN, and note its UUID + SubmissionUid.
--      The server log line to search for is:  "Captured UUID:" / "Accepted document(s)".
--   3. Fill one INSERT per confirmed orphan into #Reconcile in SECTION 2,
--      using values verified for THIS database (the script refuses to run
--      with an empty #Reconcile). Never reuse UUIDs across environments.
--   4. Run SECTION 2. It is idempotent and only touches rows whose UUID is
--      still NULL/empty. Background status sync will then pull the real
--      LHDN status (Valid/Invalid) on its next pass.
--   5. Any invoice NOT found at LHDN needs no fix — clear its stale claim so
--      it can be submitted normally (SECTION 3).
-- ============================================================================

------------------------------------------------------------------------------
-- SECTION 1 — Report candidate orphans (claimed for submission, but no UUID)
------------------------------------------------------------------------------
SELECT  InvoiceNo, DocTypeCode, InternalStatusId, LHDNStatusId,
        SubmissionClaimedAtUtc, CreatedDate, CreatedBy
FROM    InvoiceHeaders
WHERE   (UUID IS NULL OR UUID = '')
  AND   SubmissionClaimedAtUtc IS NOT NULL
ORDER BY SubmissionClaimedAtUtc DESC;

------------------------------------------------------------------------------
-- SECTION 2 — Apply confirmed reconciliations (idempotent, UUID-guarded)
------------------------------------------------------------------------------
CREATE TABLE #Reconcile
(
    InvoiceNo     NVARCHAR(50)  NOT NULL PRIMARY KEY,
    Uuid          NVARCHAR(50)  NOT NULL,
    SubmissionUid NVARCHAR(50)  NOT NULL
);

-- Fill in ONE ROW PER CONFIRMED ORPHAN — for THIS database only. UUID/SubmissionUid
-- must come from LHDN/the server log of THIS environment; staging and production share
-- the EINV1xxxxx numbering, so never reuse values across environments.
--
-- Example — the known STAGING orphans (log window 2026-07-10 00:41–00:42 MYT).
-- Uncomment ONLY when running against the STAGING database, after verifying at LHDN:
-- INSERT INTO #Reconcile (InvoiceNo, Uuid, SubmissionUid) VALUES
--  ('EINV100360', '36PRSRMPEE40X7EXFMD3W3XK10', 'ES1RQGC7Q42S4HG5EMD3W3XK10'),
--  ('EINV100361', 'FH5HBJ6BDYCWTN3KN0E4W3XK10', 'P4H265F70SJXXVY2N0E4W3XK10');

IF NOT EXISTS (SELECT 1 FROM #Reconcile)
BEGIN
    DROP TABLE #Reconcile;
    RAISERROR('No reconciliation rows filled in — nothing to do. Fill #Reconcile with verified UUIDs for THIS environment first.', 16, 1);
    RETURN;
END

SET XACT_ABORT ON;  -- any error aborts and rolls back the whole transaction (no partial commits)
BEGIN TRANSACTION;

UPDATE  h
SET     h.UUID            = r.Uuid,
        h.SubmissionID    = r.SubmissionUid,
        h.LHDNStatusId    = 'Submitted',
        h.InternalStatusId = 'Submitted',
        h.LastUpdated     = SYSDATETIME(),
        h.UpdatedBy       = 'Reconcile-OrphanedSubmissions'
FROM    InvoiceHeaders h
JOIN    #Reconcile r ON r.InvoiceNo = h.InvoiceNo
WHERE   (h.UUID IS NULL OR h.UUID = '');   -- never overwrite an already-recorded submission

-- Audit trail: one history row per invoice actually fixed in this run.
INSERT INTO InvoiceHistories (InvoiceNo, Action, Timestamp, PerformedBy, Remarks)
SELECT  h.InvoiceNo, 'Reconciled', SYSDATETIME(), 'Reconcile-OrphanedSubmissions',
        CONCAT('Backfilled UUID ', h.UUID, ' / SubmissionUID ', h.SubmissionID,
               ' (accepted at LHDN but not persisted due to submission-guard rowversion bug)')
FROM    InvoiceHeaders h
JOIN    #Reconcile r ON r.InvoiceNo = h.InvoiceNo
WHERE   h.UUID = r.Uuid
  AND   h.UpdatedBy = 'Reconcile-OrphanedSubmissions'
  AND   NOT EXISTS (SELECT 1 FROM InvoiceHistories x
                    WHERE x.InvoiceNo = h.InvoiceNo AND x.Action = 'Reconciled');

COMMIT TRANSACTION;

SELECT InvoiceNo, UUID, SubmissionID, LHDNStatusId, InternalStatusId
FROM   InvoiceHeaders
WHERE  InvoiceNo IN (SELECT InvoiceNo FROM #Reconcile);

DROP TABLE #Reconcile;

------------------------------------------------------------------------------
-- SECTION 3 — Optional: an orphan candidate that does NOT exist at LHDN just
-- has a stale claim; clear it so it can be submitted normally again.
------------------------------------------------------------------------------
-- UPDATE InvoiceHeaders
-- SET    SubmissionClaimedAtUtc = NULL
-- WHERE  InvoiceNo = '<invoice-no>'
--   AND  (UUID IS NULL OR UUID = '');
