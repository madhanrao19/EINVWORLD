# EINVWORLD — Document Retention Policy

## Statutory basis

Section 82A(1) of the Malaysian Income Tax Act 1967 requires every person required to submit a tax return
to *"retain and securely keep sufficient documentation for a period of seven years from the end of the
year of assessment"*. Non-compliance carries a fine of RM300–RM10,000 or up to 12 months' imprisonment,
and LHDN investigations can reach back up to 12 years. **LHDN's own MyInvois portal retains submitted
documents for a limited window only (its search/lookup API has roughly a 31-day practical window) — it is
not a substitute system of record.** EINVWORLD, as the intermediary, is the durable record for this
requirement.

## What EINVWORLD retains, and for how long

**Invoice and submission records are retained indefinitely by default — no code path in this application
ever purges them.** Specifically:

| Data | Where it lives | Purge behaviour |
|---|---|---|
| `InvoiceHeaders` / `InvoiceLines` (the invoice itself, all fields, all statuses) | SQL Server | **Never deleted** by any scheduled job. Only ever removed by an explicit, deliberate admin action (e.g. deleting a draft before submission) — no background process purges submitted/valid/cancelled invoices. |
| `SubmissionRecords` (payload hash, LHDN response, signing state — the idempotency/dedup record) | SQL Server | **Never deleted.** |
| Signed UBL document, generated PDF | Filesystem (`Documents`/`pdf` folders configured via `FilePathConfig`) | **Never deleted** by any scheduled job. |
| LHDN validation metadata (UUID, `LongId`, timestamps, status history) | `InvoiceHeaders` columns | **Never deleted.** |
| Tamper-evident audit trail (`AuditLog`, hash-chained) | SQL Server | **Never deleted.** Append-only by design (`Services/Audit/AuditService.cs`). |
| Application/diagnostic logs (`SystemLogs`) | SQL Server | **Purged after `LogCleanupSettings:RetentionDays`** (default 365 days), via `Services/Background/LogCleanupService.cs`. This is diagnostic noise only — it is never the invoice record. |
| Expired LHDN OAuth tokens (`LHDNTokens`) | SQL Server | Removed once expired/superseded by `TokenRenewalService` — operational token cache, not a compliance record. |
| Smart Capture uploaded source file + OCR/AI extraction JSON (`SmartCaptureDocuments`) | Filesystem (`FilePathConfig:SmartCaptureFolder`) + SQL Server | **Not a compliance record — tiered, deliberate cleanup**, distinct from everything else on this page: 14 days if extraction/review failed with no draft, 30 days if a review was never actioned, 180 days once a draft exists but the invoice remains a Draft, and the full 7-year (2555-day) tier once the resulting invoice reaches a submitted/terminal LHDN status (`SmartCaptureRetentionJobHandler`, config `SmartCapture:RetentionDays*`). This is the *source upload* — once Smart Capture produces a draft, the actual `InvoiceHeaders` row it creates is retained indefinitely like every other invoice above; only the original file and the raw extraction result are subject to this shorter tiered schedule. |

**Net effect: the de facto retention of every invoice, its signed document, and its LHDN validation
response already exceeds the 7-year statutory minimum — indefinitely, by default, with no configuration
needed to achieve it.** This document exists to make that guarantee **explicit and auditable**, not to
change behaviour.

## What this policy does *not* currently guarantee

Being honest about the gap between "de facto never deleted" and a formal immutable-archive guarantee:

- **No WORM/immutability control.** SQL rows are technically `UPDATE`/`DELETE`-able by anyone with
  database access (a DBA, a future admin feature, a compromised credential). The hash-chained audit trail
  (`AuditService.VerifyChainAsync`) detects tampering with *audited actions*, but does not itself prevent a
  direct database edit to an `InvoiceHeaders` row bypassing the application.
- **No independent cold-storage/archive tier.** Retention today is "the live operational database plus
  its backups" (see `DEPLOY-NOTES.md` for the daily full-DB + log backup schedule), not a separate,
  write-once archive distinct from the production data store.
- **Backups are not immutability.** A backup can still be lost to backup-rotation policy if the live row
  were ever deleted; this policy does not by itself protect against an operator error that deletes both.

## Operator responsibility

1. **Never** manually delete rows from `InvoiceHeaders`, `InvoiceLines`, or `SubmissionRecords` in
   production, and never shorten `LogCleanupSettings:RetentionDays` in a way that could be confused with
   invoice retention (it only ever affects `SystemLogs`).
2. Follow the backup schedule and monthly restore test in `DEPLOY-NOTES.md` — a backup restore is
   currently the practical recovery path if data is ever lost.
3. If your organisation requires a *stronger* guarantee (SQL Server Temporal Tables, an off-site
   write-once archive, or legal hold tooling), treat that as a scoped follow-up project — it is not
   implemented today.
