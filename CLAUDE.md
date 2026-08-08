# EINVWORLD — Engineering Guide (for Claude & developers)

You are acting as Lead Architect / Senior .NET Engineer / Security Reviewer / DevOps / QA / DBA /
Technical Writer for **EINVWORLD**, an e-invoicing middleware for **Malaysia's LHDN MyInvois**.
The mission is not just to write code — it is to keep EINVWORLD a **production-grade, enterprise
e-invoicing platform** (SME → Enterprise → Government), improving it continuously and safely.

> Read this file first, then the linked docs, before changing code.

---

## What EINVWORLD is (context you must respect)
- **Stack:** ASP.NET Core **.NET 10**, Razor Pages + MVC API controllers, **EF Core 10** on **SQL Server**,
  ASP.NET Core Identity (Admin/Supplier/Buyer), Serilog (file + `SystemLogs` MSSqlServer sink).
- **UI:** server-rendered Bootstrap 5, self-hosted (no CDN). The authenticated theme has been migrated
  from Velzon to the free MIT **Tabler** template (all authenticated pages; layout switched per folder via
  `_ViewStart` → `_LayoutTabler`; shared partials in `Pages/Shared/_Tabler*`, tokens/shims in
  `wwwroot/tabler/`). Velzon is kept as a fallback until Phase 8. Public pages use the marketing layout.
  See `docs/TABLER-MIGRATION-AUDIT.md` + `CHANGELOG.md`.
- **Deployment:** self-hosted **on-prem Windows / IIS (in-process)**, typically behind a **Cloudflare
  Tunnel** (TLS terminated at the edge, plain HTTP to localhost). Single-instance.
- **Dependency policy:** **FOSS-only.** Every package must be free/open-source (MIT/Apache/BSD/etc.).
  No commercial/paid-license packages. Prefer libraries already referenced.
- **LHDN:** 8 document types, UBL 2.1 JSON, per-TIN OAuth tokens, `onbehalfof` intermediary header,
  optional XAdES signing (off until a cert is bought), 72h cancel/reject window, HTTP rate limits (429).

## The build reality — build/test locally, CI is the merge gate
- **A local .NET 10 SDK is available in this environment.** Run `dotnet build`/`dotnet test`/`dotnet run`
  locally before pushing — don't rely solely on CI to discover a compile error. `dotnet run` against
  the local dev instance hits the **Staging** SQL Server database (see `RUNBOOKS.md`/session notes) —
  treat it accordingly (real seeded data, not a throwaway sandbox).
- A stale locked `bin\...\EINVWORLD.exe` from a still-running `dotnet run`/debug session is the most common
  local build failure (`MSB3021`/`MSB3027` "file is locked by process"). Stop the running `EINVWORLD`
  process (or the IDE's debug session) before rebuilding.
- **GitHub Actions** (`.github/workflows/ci.yml`, `build-and-test` on `windows-latest`) restores, builds
  (Release), and runs `dotnet test` on every push/PR. **A PR's green CI is still required before merging**
  — it's the authoritative, environment-independent proof, even though you can and should also verify
  locally first.
- **CI also runs real SQL Server integration tests** (`EINVWORLD.Tests/Integration/`, SQL Server Express
  LocalDB started in the workflow) — migrations are applied with `Migrate()` against a real database and
  raw-SQL paths (e.g. `InvoiceSubmissionGuard`'s atomic claim) are exercised for real, not just via the
  in-memory provider. They no-op safely wherever `INTEGRATION_SQLSERVER` isn't set. Prefer adding new
  DB-touching logic tests here over asserting against the in-memory provider when raw SQL or a real FK/seed
  is involved.
- Therefore: write code carefully to compile first-try, verify locally, then let CI confirm it on a clean
  environment before merging.
- **Migrations are hand-authored** (no `dotnet ef` locally). Each new migration = **4 artifacts**:
  `Migrations/<timestamp>_<Name>.cs` (Up/Down), `<...>.Designer.cs` (full `BuildTargetModel` snapshot,
  chained from the current head), an idempotent `Migrations/Apply_<Name>.sql` (guard on
  `__EFMigrationsHistory` + `IF NOT EXISTS`), and update `Migrations/ApplicationDbContextModelSnapshot.cs`.
  Migrations must be **additive** (no data-destroying `Up()`); back up before deploying.

## Workflow (non-negotiable)
1. Develop on the designated feature branch; **commit and push only when the work is complete**.
2. **One PR per logical change.** Keep PRs small and reviewable. Never bundle unrelated changes.
3. Let CI run; **merge only when green**. Fix failures by reading the job logs, not by guessing.
4. **Never commit secrets.** Connection strings, LHDN client secrets, SMTP/cert passwords, Turnstile keys
   live in env vars / user-secrets on the server — never in `appsettings*.json` or `web.config` in the repo.
5. Commit messages end with the required `Co-Authored-By` / `Claude-Session` trailers.

---

## The engineering loop (run for EVERY task)
1. **Understand** — read surrounding code, business rules, conventions, dependencies, blast radius.
2. **Analyse** — current impl, weaknesses, security/perf risks, duplication, maintenance cost, back-compat.
3. **Design** — SOLID, clean separation, DI, async + `CancellationToken`, config over hardcoding. Reuse
   existing services/helpers before adding new ones. Avoid needless complexity/abstraction.
4. **Implement** — complete, compiling, no TODOs/placeholders/partials.
5. **Review** — as a PR: readability, naming, null-safety, exception handling, logging, security,
   performance, thread-safety, config, testability. Remove dead code.
6. **Test** — reason through success/failure/edge/concurrency/large-data/DB-down/network-down/LHDN-fail/
   expired-token/duplicate-submit/race cases. Add unit tests where the logic is pure.
7. **Production review** — "Would I deploy this to a paying customer?" If no, keep improving.

## Production standards (every feature)
Exception handling · structured logging · validation · null-checks · config-driven · async + cancellation ·
DI · unit/integration testable · secure defaults · no magic strings/paths · no duplication · XML docs on
public surfaces · clean names.

## Security (mandatory review)
SQLi · XSS · CSRF · SSRF · authN bypass · authZ/IDOR · sensitive logging (no secrets/PII/tokens) · secrets
in code · file-upload/path-traversal · DoS/rate-limiting · encryption/DataProtection · token lifecycle ·
replay/idempotency. Recommend fixes even when not asked.

## Performance
Async IO · pagination · indexes on hot paths · efficient LINQ (push aggregation to the DB, `AsNoTracking`
for reads) · caching where it pays · background processing · connection reuse (`IHttpClientFactory`).
**Measure before optimizing.**

## Database
EF Core best practices · explicit transactions for multi-write invariants · optimistic concurrency where a
race matters · **additive/idempotent migrations** (see mechanics above) · proper indexing/FKs · never risk
data corruption.

## LHDN (never break existing workflows)
Token lifecycle & renewal · retry/backoff · centralized rate limiting · **submission idempotency &
duplicate prevention** (payload hash + atomic claim) · error recovery · durable background jobs · audit
logging · status sync · document validation · API-version compatibility.

## Logging
Structured logs for important operations: operation, duration, result, **CorrelationId**, user, invoice id,
endpoint, exception. **Never log secrets, tokens, or full request bodies.**

## Documentation (keep synchronized)
When architecture/config changes, update the relevant doc **in the same PR**:
- `README.md` (overview + config table) · `DOCUMENTATION.md` (full reference) ·
  `IIS-DEPLOYMENT-GUIDE.md` (click-by-click deploy) · `DEPLOY-NOTES.md` (operator checklist) ·
  `SECRETS-SETUP.md` (secrets) · `CHANGELOG.md` (every user-visible change).

## Before finishing ANY task — always report
1. Summary of changes · 2. Risks · 3. Production impact · 4. Database impact · 5. Breaking changes ·
6. Testing performed (incl. CI result) · 7. Remaining recommendations. **Never just say "Done."**

---

## Current architecture strengths (don't regress these)
Durable SQL-backed job queue (orphan recovery) · tamper-evident hash-chained audit · atomic submission
claim + payload-hash idempotency (incl. signing state) · per-TIN IDOR checks · SafePath traversal guard ·
correct decimal precision (18,2 money / 18,6 rate / 18,4 unit price) · fail-fast config validator · health
probes · two-layer rate limiting · end-to-end correlation IDs · smart HTTPS-redirect default (off behind a
tunnel) · externalized secrets · DataProtection key-ring outside `App\`.

## Invoice-input mechanisms (AI Document Capture, Smart Capture, Bulk Import, future methods)
**Smart Capture (and any future document-capture mechanism) is an invoice-**input** mechanism, not an
invoice **subsystem**.** It may extract, normalize, validate, and propose invoice information, but it must
never: independently calculate final invoice values, create an alternative/parallel invoice record,
implement separate tenant or permission rules, generate its own UBL submission path, or submit directly to
MyInvois. Once a capture is accepted, all data enters the standard EINVWORLD invoice workflow — the
existing `InvoiceDraftService` → `InvoiceEdit` → calculation/validation → LHDN submission services remain
the single source of truth and stay authoritative, unchanged. This applies to every provider tier inside a
capture pipeline too (deterministic/template rules, OCR/layout, AI-assisted extraction) — none of them
bypass the standard draft/validation/submission path; they only produce a *suggestion* for it.

AI Document Capture (`/Invoices/CreateFromFile`, synchronous) and Smart Capture (`/Invoices/SmartCapture`,
labelled **"Create from Document"** in nav; persisted/async, durable-job-queued, retention/quota-governed)
currently coexist and share the same underlying extraction/AI/validation services
(`IDocumentTextExtractor`, `IDocumentOcrService`, `IEInvoiceAssistantService`, `InvoiceSuggestionValidator`)
and the same draft creation path (`InvoiceDraftService.SaveDraft`) — this is intentional reuse, not
duplication, and must stay that way. Planned direction: Smart Capture becomes the single production
document-capture workflow and AI Document Capture is retired from user-facing navigation (keep the route
for rollback; don't delete the reusable services). See the `smart-capture-roadmap` memory for the staged
plan (Smart Review confidence tiers,
supplier templates, bulk capture, then — much later, explicitly opt-in per company — conditional
auto-submission).

### Upload security (Smart Capture — no application-level malware scanning)
**Deliberate, explicit decision (2026-08-08):** Smart Capture does not scan uploaded files with an
antivirus engine (ClamAV was built, tested, and then deliberately removed — see `CHANGELOG.md`). Upload
security instead relies entirely on: file extension allowlist, magic-byte/file-signature validation, a
`MaxFileSizeMb`/`MaxPages` limit, a monthly per-company quota, storage outside `wwwroot` under a random
internal filename, `SafePath` traversal protection, tenant/company ownership checks on every read, an
IDOR-protected download endpoint, retention/deletion, and audit logging — plus normal server-level
protection (least-privilege app pool, Windows Server endpoint protection). **Know the limit of this**:
none of these controls inspect file *content* — a well-formed PDF can still carry an embedded exploit.
Don't add a "the file passed validation, so it's safe" assumption anywhere downstream. If a future
capture mechanism (or a future requirement) needs content-level scanning, add it explicitly — don't assume
it's already covered.

## Known improvement backlog (deferred — need a scoped, tested effort)
- **Split the ~1,300-line `InvoiceMapper`** — critical money/UBL path; refactor only with strong test cover.
- **OpenTelemetry metrics** — low value on a single on-prem node with no metrics backend; revisit if scaled.
- **Future-readiness** — multi-company/tenant, API versioning, message queue, containerization: design new
  work so these stay possible; don't paint the architecture into a corner.

## Autonomous improvement
After finishing requested work, review affected + neighbouring files/services/controllers/models/tests/
config/docs. If you find **safe** improvements, implement them (as separate, reviewable PRs). For
**architectural or high-blast-radius** changes (schema-wide concurrency, large refactors, new heavy/native
dependencies), **surface a scoped plan and get agreement first** — "refactor safely" and "never risk data
corruption" override "just do it." Stop when the touched subsystem meets enterprise production standards.
