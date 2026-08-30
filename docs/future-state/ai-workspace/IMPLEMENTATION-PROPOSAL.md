# eInvWorld AI Workspace — phased implementation proposal

**Status: proposal only. No phase below may be started without explicit review and approval.**
Reference UX: [`DESIGN.md`](./DESIGN.md) / [`mockup.html`](./mockup.html) — visual direction only;
none of the mockup's fake data, CDN dependencies, or client-only simulation ship as-is (see
[`README.md`](./README.md)).

## Ground truth: what already exists

The mockup reads as a request for a brand-new AI product. Most of the hard part already exists in
this codebase — the work is mostly UI shell + wiring, not new AI plumbing:

| Mockup concept | Existing component |
|---|---|
| Chat copilot pane, Q&A | `Pages/Assistant/Index.cshtml(.cs)` + `IEInvoiceAssistantService.AskAsync` (multi-turn, `[Authorize(Roles="Admin,Supplier")]`) |
| "Draft from prompt" | `IEInvoiceAssistantService.SuggestInvoiceAsync` → structured JSON, buyer picked from the caller's real customers, never auto-submits |
| Explain a rejection | `IEInvoiceAssistantService.ExplainRejectionAsync` |
| Hallucination guard / readiness check | `IEInvoiceAssistantService.ReviewSuggestion` — validates a suggestion against real LHDN reference data before it reaches the form |
| Save/update a draft | `Services/InvoiceDraftService.cs` |
| Double-submit protection | `Helpers/InvoiceSubmissionGuard` — atomic DB claim, 5 min stale-reclaim |
| Tamper-evident audit trail | `Services/Audit/AuditService` — hash-chained, append-only |
| Provider abstraction | `IAiService` (Ollama today, swappable) — `EInvoiceAssistantService` never talks to Ollama directly |
| Gating | AI is **off by default**; `Enabled` comes from `_assistant.IsEnabled` (config-gated, per [[ai-assistant-local-ollama]] memory) |
| Per-tenant TIN checks | `Helpers/GetSubmittingTin.cs` + `TinHelper` (tested in `TinHelperTests.cs`), `Models/InputModel/UserCompany.cs` for multi-company-per-user |

Every phase below is framed as **extend this**, not build from zero.

## Cross-cutting constraints (apply to every phase)

- **FOSS-only.** No new paid/commercial packages. The mockup's Tailwind CDN + Google Fonts CDN are
  explicitly out — any UI work uses the existing self-hosted Bootstrap/Tabler stack and
  `wwwroot/tabler/css/einvworld-tokens.css` tokens.
- **AI stays advisory, never autonomous.** Every existing assistant method returns a suggestion for
  a human to accept/reject — nothing in the assistant service submits to LHDN, finalizes an
  invoice, or mutates a saved record on its own. No phase below changes that invariant.
- **LHDN safeguards are non-negotiable.** `InvoiceSubmissionGuard`'s atomic claim, the payload-hash
  idempotency check, and the existing 72h cancel/reject window logic must not be bypassed or
  duplicated by a new "AI workspace" submission path — the workspace calls the *same* finalize/
  submit code path invoices already use today, it doesn't grow a second one.
- **Tenant isolation.** Every new read/write must go through the existing per-TIN / per-company
  authorization checks (`GetSubmittingTin`/`TinHelper`, `UserCompany`) — an IDOR here would let one
  supplier's AI session see or suggest against another supplier's invoice or customer list.
- **Migrations are additive**, hand-authored (4 artifacts per `CLAUDE.md`), never data-destroying.
- **Every phase ships with tests** — unit tests for new pure logic, and the SQL Server integration
  tests (`EINVWORLD.Tests/Integration/`) for anything touching a real DB write/claim.

---

## Phase 1 — Unified assistant shell (existing gated Ollama assistant)

**Goal:** Replace the current single-purpose `Pages/Assistant/Index` postback form with a
persistent, dockable chat panel (mockup's "Finance Copilot" column) usable from the invoice
pages — same backend, better shell. No new AI capability.

- **Reuse:** `IEInvoiceAssistantService` unchanged (`AskAsync`, multi-turn history). `Enabled`/
  `IsSoftError` gating pattern from `IndexModel` reused as-is.
- **New:** A Razor partial (`_AssistantPanel`) + a small JS controller replacing full-page postback
  with `fetch()` calls to a thin Razor Page handler (or existing page handler reused via AJAX) so
  the panel can live alongside `CreateInvoice`/`InvoiceEdit` without a page navigation. No SSE/
  streaming in this phase — request/response, matching how `AskAsync` already works.
- **DB changes:** none. Chat history already round-trips via `ChatHistoryJson`; no new persistence
  needed for a same-session panel.
- **AuthZ:** same `[Authorize(Roles = "Admin,Supplier")]` as today; panel only renders on pages
  already behind that role check.
- **Tenant isolation:** N/A — no invoice-specific data flows through this phase yet (Phase 2 adds
  it).
- **LHDN safeguards:** N/A — pure Q&A, no draft/submit interaction yet.
- **Tests:** existing `AiServiceTests.cs`/`OllamaAiProviderTests.cs` cover the service; add a
  Playwright spec for panel open/close/ask/response-render (mock or skip gracefully if Ollama isn't
  enrolled in the test environment, matching the existing `10-tabler-modules.spec.js` tolerance
  pattern).
- **Dependencies:** none new.
- **Acceptance criteria:** panel opens/closes without page reload; a question round-trips to the
  same `AskAsync` backend and renders the answer; feature-off state shows the existing soft-disabled
  message instead of the panel; no behavior change to `Pages/Assistant/Index` (kept working during
  transition, removed/redirected only after the panel fully replaces it).

## Phase 2 — Real invoice drafting into existing draft services

**Goal:** Wire "draft from a plain-English prompt" into the panel, writing through the *existing*
draft path — no parallel invoice-creation logic.

- **Reuse:** `SuggestInvoiceAsync` → `ReviewSuggestion` (hallucination/reference-data guard) →
  populate the **same** `InvoiceHeaderView` model `CreateInvoice.cshtml.cs` already binds → save via
  `InvoiceDraftService.SaveDraft`. The AI produces the same shape of data a human typing into the
  form would.
- **New:** a mapping layer from the assistant's suggestion JSON to `InvoiceHeaderView` (thin — most
  of this already exists conceptually in `CreateFromFile.cshtml.cs`, which also consumes
  `IEInvoiceAssistantService`; audit that file first, this phase may be largely reuse).
- **DB changes:** none expected — writes go through `InvoiceHeaders`/`InvoiceLines` via the existing
  service. If provenance isn't needed yet (that's Phase 5), no schema change here.
- **AuthZ:** unchanged; `knownBuyers` passed to `SuggestInvoiceAsync` must be scoped to the
  authenticated supplier's own customers (already how `CreateFromFile` does it — confirm reused,
  not re-derived).
- **Tenant isolation risk:** the biggest new risk in this phase is a prompt like "invoice for
  &lt;customer name&gt;" resolving to the wrong tenant's customer record if the buyer lookup isn't
  scoped correctly — must reuse the exact scoping `CreateFromFile.cshtml.cs` already uses, not a new
  query.
- **LHDN safeguards:** draft only; `InvoiceSubmissionGuard`/submit path untouched by this phase.
- **Tests:** extend `InvoiceSuggestionValidatorTests.cs`; add a test asserting a suggestion can only
  resolve buyers from the caller's own `knownBuyers` list (regression guard for the IDOR risk
  above).
- **Dependencies:** none new.
- **Acceptance criteria:** a prompt produces a draft indistinguishable (in the DB) from one entered
  manually; `ReviewSuggestion`'s readiness checklist renders in the panel before the draft is saved;
  rejecting the suggestion discards it with no DB write.

## Phase 3 — Field-level AI suggestion tracking (accept / reject / undo)

**Goal:** The mockup's per-field "Accept / Reject / Undo" + diff display. This is the first phase
that needs new state — Phases 1–2 are stateless relative to the DB.

- **New backend component:** a suggestion-tracking table, e.g. `InvoiceFieldSuggestions`
  (`InvoiceNo`, `FieldName`, `OldValue`, `NewValue`, `Source` [AI/user], `Status`
  [Pending/Accepted/Rejected/Undone], `CreatedAtUtc`, `DecidedByUserId`, `DecidedAtUtc`). One row
  per proposed field change, not a JSON blob — needed for accept/reject/undo and for the Phase 5
  audit trail to query cleanly.
- **New service:** `IInvoiceSuggestionTrackingService` (accept/reject/undo transitions; undo is a
  new row reverting `NewValue`→`OldValue`, not a delete — keeps history intact for audit).
- **DB changes:** **one new additive migration** (4 artifacts per `CLAUDE.md`): new table, FK to
  `InvoiceHeaders.InvoiceNo`, index on `(InvoiceNo, Status)`.
- **AuthZ:** accept/reject/undo must verify the acting user owns/has role access to the invoice's
  TIN — reuse the same per-TIN check `InvoiceEdit.cshtml.cs` already applies before allowing edits.
- **Tenant isolation risk:** an IDOR here (accepting a suggestion on someone else's invoice by
  guessing/enumerating a suggestion ID) is the same class of bug the codebase's existing "per-TIN
  IDOR checks" pattern was built to prevent — this phase must reuse that pattern, not invent a
  parallel check.
- **LHDN safeguards:** suggestions only apply to *draft* invoices (not yet submitted); reject any
  accept/reject/undo call against an invoice that already has a UUID (already submitted) or an
  active `InvoiceSubmissionGuard` claim.
- **Tests:** unit tests for the state machine (pending→accepted/rejected/undone, invalid
  transitions rejected); integration test against real SQL Server for the FK/index and for a
  concurrent accept+submit race (accept must lose if a submission claim is in flight).
- **Dependencies:** none new.
- **Acceptance criteria:** each AI-touched field shows its own accept/reject/undo affordance;
  accepting commits `NewValue` to the invoice record; undo after accept restores `OldValue` and is
  itself an auditable event; no suggestion can be actioned on a submitted invoice.

## Phase 4 — Live validation and submission-readiness integration

**Goal:** The mockup's "Readiness Check" bar and "Pre-submission Simulation" — but backed by real
checks, not a hardcoded percentage.

- **Reuse:** whatever validation already runs before `InvoiceFinalizer`/`InvoiceFinalizerService`
  submits to LHDN (mandatory-field checks, tax calculation, schema validation) — this phase
  **surfaces** those checks earlier and incrementally in the UI, it does not invent new validation
  rules.
- **New:** a read-only "readiness" endpoint/service that runs the existing pre-submit validation
  against the current draft state (including any Phase 3 pending suggestions) and returns a
  checklist — no new validation logic, a query-mode wrapper around what `InvoiceFinalizer` already
  checks before it would submit.
- **DB changes:** none expected (read-only against existing draft + suggestion tables).
- **AuthZ/tenant isolation:** inherits from the draft's existing access checks; no new surface.
- **LHDN safeguards:** this is explicitly **read-only simulation** — it must never call the real
  LHDN submit endpoint, and the UI must not present "98% success" as a guarantee. If the mockup's
  confidence-percentage framing is kept, it must be computed from real rule-pass/fail counts, not a
  cosmetic number.
- **Tests:** unit tests asserting the readiness checklist matches what `InvoiceFinalizer` would
  actually reject/accept for a given draft state (shared logic, not a duplicate rule set — if this
  can't be shared cleanly, that's a sign this phase should refactor the validation out of
  `InvoiceFinalizer` into a callable checker both paths use, rather than fork it).
- **Dependencies:** none new.
- **Acceptance criteria:** readiness checklist reflects real validation state; a draft that fails a
  check here also fails (or is blocked from) actual submission; no separate/divergent rule set
  between "readiness preview" and "actual submit."

## Phase 5 — Evidence, provenance, audit trail and role-based review

**Goal:** The mockup's "View Traceability Evidence" and "Collaborative Timeline" — grounded in the
existing hash-chained audit log, not a new logging system.

- **Reuse:** `Services/Audit/AuditService` (`WriteAsync`) — every AI suggestion, accept, reject, and
  undo from Phase 3 already needs an audit row; this phase is largely about **querying and
  rendering** that existing chain for a given invoice, plus adding a few new `Action` values
  (`AI.SuggestionGenerated`, `AI.SuggestionAccepted`, etc.) to the existing audit vocabulary.
- **New:** "evidence" for a suggestion (why the AI proposed this value — e.g. "matched 142 prior
  invoices") needs a small structured field on `InvoiceFieldSuggestions` (from Phase 3) or a
  companion table if evidence is multi-item — **do not fabricate evidence**: only record what the
  assistant's prompt/response actually grounded the suggestion in (e.g. cite the reference-data
  match `ReviewSuggestion` already performs), never a made-up "confidence score" or invented
  citation list like the mockup's static example.
- **DB changes:** additive migration if a new evidence table/column is needed; otherwise reuse
  Phase 3's table.
- **AuthZ:** role-based review — confirm which roles (Admin/Supplier/Buyer) may view vs. action
  another user's pending suggestions on a shared invoice; likely Admin + the owning Supplier only,
  matching existing invoice-edit role gates.
- **Tenant isolation:** audit queries for the timeline must filter by the same TIN scoping as
  everything else — no cross-tenant audit leakage.
- **LHDN safeguards:** audit entries for AI actions must never log secrets/tokens/full request
  bodies, consistent with existing `AuditService` usage and `CLAUDE.md`'s logging rules.
- **Tests:** unit test that every Phase 3 state transition produces exactly one audit row; a query
  test that the rendered timeline matches the audit chain for a sample invoice.
- **Dependencies:** none new.
- **Acceptance criteria:** timeline in the UI is generated from real `AuditLog` rows (not mock
  data); every AI suggestion's "evidence" traces to something the assistant actually used (reference
  data match, prior-value diff) — no invented percentages or citations.

## Phase 6 — Collaboration, approval workflow and advanced automation

**Goal:** Multi-user review handoff (mockup's "Reviewing changes from AI & Sarah") and any
further automation. **This phase is the least defined and carries the most architectural risk** —
it should get its own scoped design/roast pass before estimation, not be estimated now.

- **Open questions to resolve before scoping:** does "collaboration" mean real-time (would need a
  new transport — SignalR is the natural FOSS fit already available in ASP.NET Core, no new
  dependency) or async (poll-based, reusing existing patterns, no new dependency)? Does "approval
  workflow" need a new role/permission tier beyond Admin/Supplier/Buyer, or map onto existing roles?
- **Reuse candidates:** Phase 3's suggestion table for "who decided what"; Phase 5's audit trail for
  "who did what when"; existing role model as the starting point for approval gating.
- **DB changes:** likely additive (an approval-state table), but undefined until the workflow is
  specified.
- **AuthZ/tenant isolation/LHDN safeguards:** cannot be assessed until the workflow (who approves,
  at what stage, with what override authority) is defined — approval logic that can bypass
  `InvoiceSubmissionGuard` or the 72h cancel/reject window would be a serious regression, so this is
  the phase most likely to need explicit sign-off on the workflow *design* before any code.
- **Tests/dependencies/acceptance criteria:** deferred — not assessable until scope is fixed.

---

## Sequencing note

Phases 1–2 are additive UI/wiring on top of services that already exist and already work
standalone — lowest risk, could ship independently of the rest. Phase 3 is the first phase with new
persistent state and is the real gating decision: everything from Phase 4 onward assumes Phase 3's
suggestion table exists. Phase 6 should not be scoped in detail until Phases 1–5 are built and in
use — its shape depends on how real users actually use field-level review in practice.

**No phase starts without a separate roast → plan → approval cycle, per `CLAUDE.md`.**
