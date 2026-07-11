# EINVWORLD — Velzon → Tabler UI Migration Audit (Phase 1)

> ## Progress log (updated 2026-07-10)
> | Phase | Status | Notes |
> |---|---|---|
> | 1 — Audit | ✅ done | this document |
> | 2 — Parallel scaffolding | ✅ merged | Tabler v1.4.0 assets + `_LayoutTabler` |
> | 3 — Pilot (`Items/Index`) | ✅ merged | first opt-in page |
> | Foundation partials | ✅ merged | decomposed `_TablerSidebar`/`_TablerTopbar`/`_UserMenu`/nav/`_Footer`/`_PageHeader` + tokens + UI helpers |
> | 4 — Low-risk folders | ✅ merged | Items, Suppliers, PublicCustomer, Lead, Profile, RecurringInvoices (per-folder `_ViewStart`) |
> | 5 — Admin (40 pages) | ✅ merged | one `Pages/Admin/_ViewStart.cshtml` |
> | 6 — Dashboard + Invoices | ✅ merged | money path; **PDF/print `Layout=null` untouched** |
> | 7 — Auth (`_LoginLayoutTabler`) | ✅ merged | login/2FA/register/manage; Velzon `_LoginLayout` kept as fallback |
> | Consistency audit — orphans | ✅ merged | Templates + Assistant were the last 2 on Velzon → migrated. **All authenticated pages now Tabler.** |
> | Post-deploy staging QA + fixes | ✅ merged | logo size, code/pre overflow, invoice-list mobile columns; text verified on all Admin pages |
> | 8 — Velzon removal + theme-controller retirement + demo-bloat cleanup | ⛔ **deferred** | destructive; do only after a fully-green re-verification |
>
> **Status (2026-07-11): deployed to staging and Playwright-verified across Supplier/Buyer/Admin.** Every
> authenticated page renders Tabler; only public marketing/Home/Resources (`_HomeLayout`) and Error pages
> (`Layout = null`) are intentionally non-Tabler.
> **Rollout mechanism:** per-folder `_ViewStart.cshtml` switches a folder to `_LayoutTabler` for
> authenticated users only (anonymous/public pages keep the marketing layout). **Revert** any area by
> deleting its `_ViewStart.cshtml`. Velzon utility classes are shimmed in
> `wwwroot/tabler/css/einvworld-tokens.css` so no per-page markup was rewritten.
> **To re-verify after any deploy:** set Turnstile **test** keys
> (`Turnstile__SiteKey=1x00000000000000000000AA`, `Turnstile__SecretKey=1x0000000000000000000000000000000AA`)
> and temporarily `Security__EnforceAdminMfa=false`, then run `tests/playwright/10-tabler-modules.spec.js`
> (revert those env vars after). **Known residual:** AI Settings minor mobile overflow. **Pre-existing app
> bugs surfaced by QA (not Tabler):** company logos emitted as `file:///` paths (Suppliers/Index) and
> missing resource images (404).

**Status:** Audit only. No application code changed in this phase.
**Date:** 2026-07-10 · **Author:** Lead Architect (engineering audit)
**Scope:** Replace the Velzon Bootstrap 5 admin theme on the **authenticated** UI with the free MIT
[Tabler](https://tabler.io) Bootstrap 5 template. Stay server-rendered Razor Pages. Preserve all
backend/LHDN/security/PDF behaviour. Keep Remix Icons and all functional plugins working.

> **Roast verdict: RESHAPE → GREEN LIGHT (audit-only phase approved).** The migration is feasible and
> low backend-risk *because* the theme is almost entirely static assets + markup classes, but it is not
> "swap a CSS file". There is one real backend coupling (the DB-backed global theme system) and a large
> hand-authored surface (160 views). The parallel-layout + pilot approach is sound and is the only safe
> way to do it. Do **not** attempt a big-bang swap.

---

## 0. Executive summary

| Metric | Finding |
|---|---|
| Authenticated Razor views in scope | **~160** `.cshtml` (Pages + Areas/Identity) |
| Primary theme surfaces | `_Layout.cshtml` (1,463 lines), `_Sidebar.cshtml`, `_LoginLayout.cshtml` |
| Backend coupling to the theme | **1 system** — `GlobalThemeService` + `ThemeController` + `GlobalThemeSettings` (DB-backed Velzon layout attributes) |
| Functional plugins to preserve | Bootstrap, jQuery, Select2, Flatpickr, SweetAlert2, Toastr (+NToastNotify), Chart.js, TinyMCE — all self-hosted, all theme-independent |
| PDF/print exposure | **None** — `PdfTemplate.cshtml` and `InvoiceDetails.cshtml` set `Layout = null` |
| Playwright coupling to Velzon chrome | **Very low** — tests key off form IDs + `select2`/`flatpickr`/`topbar`, not `layout-wrapper`/`menu-link`/`data-layout` |
| Velzon asset weight | `wwwroot/assets/libs` ≈ **60 MB** (mostly unused demo HTML + plugins) |
| Icons in use | Remix `ri-` **520** (keep) · Lineawesome **57** · FontAwesome **18** · Material `mdi-` **15** · Boxicons `bx-` **3** |
| CSP risk | Low — CSP is **Report-Only**; self-hosted assets already satisfy `'self'` |

**Bottom line:** the risk is *breadth* (many views, subtle responsive/utility-class differences), not
*backend depth*. The one thing that must be handled deliberately is the global theme system.

---

## 1. Complete page inventory (authenticated, in scope)

Counts are `.cshtml` view files (excluding `.cshtml.cs`).

| Area | Views | Notes / risk |
|---|---:|---|
| `Pages/Dashboard` | 3 | Chart.js widgets, stat cards — **medium** (JS init + grid) |
| `Pages/Invoices` | 13 | **Highest business risk** — Create/Edit/Lists/Details, Select2/Flatpickr/TinyMCE, money path |
| `Pages/Templates` | 6 | Invoice template CRUD — shares Invoice form patterns |
| `Pages/RecurringInvoices` | 3 | Scheduling UI |
| `Pages/Suppliers` | 8 | Company CRUD, logo upload |
| `Pages/PublicCustomer` | 5 | Buyer CRUD |
| `Pages/Items` | 5 | Item CRUD (has Playwright coverage — `06-crud-items`) |
| `Pages/Lead` | 6 | Customer submissions |
| `Pages/Profile` | 2 | User profile |
| `Pages/Assistant` | 1 | AI assistant (gated off by default) |
| `Pages/Admin` | 40 | Largest area: Users, Codes (9 tables), Logs, SyncJobs, AuditLog, SystemHealth, Webhooks, AiSettings, Resources, Notifications |
| `Areas/Identity/Pages/Account` | 36 | Login, 2FA, Manage/*, register/confirm/reset — uses `_LoginLayout` |
| **Total** | **~160** | |

**Marketing/anonymous pages (`_HomeLayout`) are OUT OF SCOPE** — the task targets the authenticated UI.
`Pages/_ViewStart.cshtml` routes authenticated users to `_Layout` and anonymous to `_HomeLayout`.

---

## 2. Shared layout inventory

| File | Role | Velzon coupling | Migration action |
|---|---|---|---|
| `Pages/Shared/_Layout.cshtml` | Main authenticated shell (1,463 lines) | **Heavy** — `layout-wrapper`, `main-content`, `page-content`, preloader, Admin **theme customizer offcanvas** (~950 lines of Velzon demo markup), global-theme JS, idle-timeout, app-search | Rebuild as `_LayoutTabler.cshtml` (parallel) |
| `Pages/Shared/_Sidebar.cshtml` | Topbar **and** role-based app menu | **Heavy** — `app-menu navbar-menu`, `navbar-nav`, `menu-link`, `menu-dropdown`, `topbar-user`, `light-dark-mode`, `topnav-hamburger` | Rebuild as `_SidebarTabler.cshtml` (Tabler `navbar navbar-vertical`) |
| `Pages/Shared/_LoginLayout.cshtml` | Auth pages shell | **Heavy** — `auth-page-wrapper`, `auth-one-bg`, `bg-overlay`, loads `app.min.css` | Rebuild as `_LoginLayoutTabler.cshtml` (later phase) |
| `Pages/Shared/_Navbar.cshtml` | Secondary nav partial | Medium | Review during pilot |
| `Pages/Shared/_HomeLayout.cshtml` | Anonymous marketing shell | N/A — **out of scope** | Leave untouched |
| `Pages/Shared/_Layout.cshtml.css` | Scoped layout CSS | Low | Port needed rules |
| `_LoginPartial` / `_CustomLoginPartial` / `_Navbar` / `_Favicons` / `_GoogleAnalytics` / `_TurnstilePartial` / `_ValidationScriptsPartial` / `_HelpInstructions` | Partials | Mixed | Mostly reusable as-is |

---

## 3. Velzon asset inventory (`wwwroot/assets`, ≈94 MB total)

### 3.1 Velzon **core** (theme chrome — to be replaced by Tabler)
| Asset | Purpose |
|---|---|
| `assets/css/app.min.css` | Velzon main theme CSS (layout, sidebar, topbar, customizer) |
| `assets/css/custom.min.css` | Project overrides on top of Velzon |
| `assets/css/icons.min.css` | Bundled icon fonts (Remix + Material + Boxicons + Lineawesome + FontAwesome) |
| `assets/css/bootstrap.min.css` | Velzon's Bootstrap build (Tabler ships its own) |
| `assets/js/app.js`, `layout.js`, `plugins.js`, `app-baru.js`, `app1.js` | Velzon layout/customizer JS |
| `assets/js/pages/*` | Velzon demo page initialisers (only `sweetalerts.init.js` is used) |
| `assets/images/demos/*`, `assets/images/sidebar/img-*.jpg` | Customizer preview + sidebar background images |

### 3.2 Velzon **demo bloat** (unused — cleanup opportunity, NOT Phase 1)
- **Hundreds of demo HTML files** in `assets/libs/` (`apps-*.html`, `dashboard-*.html`, `charts-*.html`,
  `auth-*.html`, `ui-*.html`, `forms-*.html`, `pages-*.html`, `icons-*.html`, etc.) — these are Velzon's
  documentation pages shipped by mistake.
- **Unused plugin libs**: `apexcharts`, `echarts`, `fullcalendar`, `leaflet`, `jsvectormap`, `gridjs`,
  `dropzone`, `filepond`, `quill`, `@ckeditor`, `swiper`, `glightbox`, `particles.js`, `nouislider`,
  `choices.js`, `cleave.js`, `list.js`, `owlcarousel`, `shepherd.js`, `aos`, `wow`, `isotope-layout`,
  `masonry-layout`, `sortablejs`, `dragula`, `jspdf`, `html2pdf`, `html2canvas`, `qrcodejs`, and more.
  These are **not referenced** by app views (server-side rendering + the plugins in §5 cover real needs).

> **Note:** demo-bloat removal is a *separate, later* cleanup PR — not part of the theme swap. It is
> listed here because the audit must inventory it, but deleting 60 MB of libs mid-migration would muddy
> the diff and the rollback story. Do it after Tabler is fully adopted.

---

## 4. JavaScript dependency inventory

### 4.1 Must keep working (functional — theme-independent, all self-hosted)
| Plugin | Load point | Notes |
|---|---|---|
| **jQuery** | `_Layout` head | Required by Select2 + inline app scripts |
| **Bootstrap** bundle | `_Layout` foot | Tabler also uses Bootstrap 5 JS — keep one copy |
| **Select2** (+ `select2-bootstrap-5-theme`) | `_Layout` | `.select2` auto-init inline; 10 Playwright touchpoints |
| **Flatpickr** | `_Layout` | Date pickers; 7 Playwright touchpoints |
| **SweetAlert2** | `_Layout` | Idle-timeout + theme-save + confirmations |
| **Toastr** (+ **NToastNotify** server-side, csproj) | `_Layout` | Flash notifications |
| **Chart.js** (+ `chartjs-plugin-zoom`) | `_Layout` head | Dashboard charts |
| **TinyMCE** (self-hosted `wwwroot/assets/js/tinymce`) | `_Layout` | Rich-text (resources/notes) |

### 4.2 Velzon-only JS (replace with Tabler equivalents)
`app.js`, `layout.js`, `plugins.js`, `simplebar`, `node-waves`, `feather-icons`, `lord-icon-2.1.0.js`,
the theme-customizer handlers, and the global-theme `fetch('/api/Theme/*')` block in `_Layout`.

### 4.3 App inline scripts to carry over (in `_Layout`, not Velzon)
Idle-timeout/auto-logout, Select2 init, global-theme apply/save, and the topbar **app-search** (calls
`/api/Search`). These must be preserved (or consciously retired for theme-save) in the Tabler layout.

---

## 5. CSS dependency inventory

| CSS | Keep / Replace |
|---|---|
| `assets/css/bootstrap.min.css` | Replace with Tabler's Bootstrap build (or Tabler `tabler.min.css` which bundles it) |
| `assets/css/app.min.css` | **Replace** with `tabler.min.css` |
| `assets/css/custom.min.css` | **Port** project-specific overrides onto a new `tabler-custom.css` |
| `assets/css/icons.min.css` | **Keep initially** (carries Remix + the long-tail icon fonts) |
| `assets/libs/select2/**`, `select2-bootstrap-5-theme/**` | Keep |
| `assets/libs/flatpickr/flatpickr.min.css` | Keep |
| `assets/libs/sweetalert2/**`, `toastr/**`, `font-awesome/**` | Keep |
| `tinymce/skins/ui/oxide/skin.min.css` | Keep (may need a dark-skin variant under Tabler) |
| `_Layout.cshtml.css` | Port required scoped rules |

**Icons:** Remix (520 uses) is dominant → self-host `remixicon` and keep. The long tail — Lineawesome
(57), FontAwesome (18), Material (15), Boxicons (3) — is currently bundled in `icons.min.css`; keep that
bundle during migration, then schedule a **later** icon-consolidation pass (Tabler ships Tabler Icons).

---

## 6. Role navigation inventory (`_Sidebar.cshtml`)

The sidebar renders a **different menu per role** (server-side `User.IsInRole`). This must be reproduced
exactly in the Tabler sidebar — it is an authorization-relevant surface (menu ≠ enforcement, but it
frames the role experience).

| Role | Menu groups |
|---|---|
| **Admin** | Dashboards · Codes Management (9 code tables) · Item Management · User Management · Lead Management · Company Management · Buyers Management · Invoice Management · Tools (Invoice Sync, Sync Jobs, Audit Trail, System Health, Webhooks, Assistant, AI Settings, AI Document Capture, Manage Resource) · System Logs |
| **Supplier** | Dashboards · My Invoice (View/Create/Assistant/Doc Capture/Templates/Bulk Import/Recurring) · Item Management · Buyers Management · My Company Management (uses per-user `companyId` from `UserCompanies`) |
| **Buyer** | Dashboards · My Invoice (View All e-Invoices only) |
| Anonymous fallback | Login link |

> Bug noted in passing (not for this phase): several Supplier/Buyer groups reuse
> `id="sidebarLead"`/`aria-controls="sidebarLead"` for different menus — duplicate IDs. Fix opportunistically
> when rebuilding the Tabler sidebar; do **not** spin a separate PR now.

---

## 7. Backend coupling — the one real risk: the global theme system

This is the only place the Velzon theme reaches into C#/DB:

- **`Controllers/ThemeController.cs`** — `GET /api/Theme/global` (called on **every authenticated page
  load**, anonymous-readable) and `POST /api/Theme/save` (`[Authorize(Roles="Admin")]`).
- **`GlobalThemeService`** — persists/reads the theme.
- **`Models/Settings/GlobalThemeSettings.cs`** — the Velzon attribute set (`data-layout`, `data-sidebar`,
  `data-theme-colors`, `data-sidebar-size`, `data-topbar`, …) stored in the DB.

Tabler does not use these attributes. **Decision required (later phase, with agreement):** during the
parallel run, keep `/api/Theme/global` returning harmless defaults so no page errors; once Tabler is the
only theme, **retire** the customizer + endpoints + service + table via an additive, reversible change
(leave the DB table in place; stop reading it). **This is the only change that touches a controller/
service/DB and therefore requires an explicit go-ahead per the "stop if backend changes" rule.**

---

## 8. Impact assessments

### 8.1 Security & authorization
- **No authZ change intended.** Menus are cosmetic; server-side `[Authorize]`/role/`OwnsTin`/
  `CanAccessInvoice` checks are untouched. Migration must not alter any handler.
- `POST /api/Theme/save` stays `[Authorize(Roles="Admin")]` while it exists.
- Preserve the Admin-only footer version string and the Admin-only customizer gating.
- **Watch item:** don't drop `__RequestVerificationToken` / antiforgery wiring when rebuilding forms.

### 8.2 CSP
- CSP is **Report-Only** (`Program.cs`) → migration cannot be *blocked* by CSP, only reported.
- Self-hosted Tabler assets satisfy `'self'`. `'unsafe-inline'`/`'unsafe-eval'` are already allowed
  (Velzon needs them). Tabler needs fewer inline hacks → a **future** CSP-tightening opportunity, not now.
- Keep Turnstile (`challenges.cloudflare.com`) and GA sources as-is.

### 8.3 Responsive / mobile
- Velzon and Tabler are both Bootstrap 5 → grid classes (`row`, `col-*`, `container-fluid`) port directly.
- **Risk is in Velzon-specific utilities** (`avatar-*`, `material-shadow`, `hstack/vstack` variants,
  `app-search`, `menu-dropdown` collapse behaviour) that Tabler names differently or omits.
- Sidebar collapse + mobile hamburger is Velzon JS (`topnav-hamburger`) → must be re-implemented with
  Tabler's offcanvas/navbar behaviour. Verify at 375 / 768 / 1440 (existing Playwright `05-responsive`).

### 8.4 IIS / publish
- Pure static-asset + `.cshtml` change; **no** `.csproj`, migration, or runtime change in the theme swap.
- Assets are already self-hosted under `wwwroot` → served by IIS static handler, offline-friendly. Adding
  Tabler = add files under `wwwroot/assets` (or `wwwroot/tabler`). No CDN introduced.
- Cache-busting: current CSS uses `?v=` query strings — keep the same convention for Tabler CSS.
- **No downtime / no app restart** required beyond a normal redeploy.

### 8.5 PDF / print
- **Zero exposure.** `PdfTemplate.cshtml` and `InvoiceDetails.cshtml` use `Layout = null` and their own
  inline styles; they never load `_Layout`/Velzon chrome. PDF (DinkToPdf/Puppeteer) is unaffected.
  *Regression guard:* after each phase, re-generate one PDF to confirm nothing changed.

---

## 9. Responsive risk by page (heat map)

| Risk | Pages | Why |
|---|---|---|
| **High** | Invoices Create/Edit/CreateSBI/CreateCN/CreateSBCN, Templates create, Bulk Import | Dense multi-column forms, dynamic line-item tables, Select2/Flatpickr/TinyMCE, money totals |
| **High** | Dashboard | Chart.js canvases + responsive stat grid |
| **Medium** | InvoiceLists / InvoiceDetails2 | Wide data tables, filters, export/print buttons, QR |
| **Medium** | Admin/Users, Admin/Codes/*, SyncJobs, AuditLog, SystemHealth | Tables + modals + badges |
| **Medium** | Login / LoginWith2fa / Manage/* | Separate `_LoginLayout`; auth is critical path |
| **Low** | Items, Suppliers, PublicCustomer, Lead, Profile, RecurringInvoices | Standard CRUD forms/lists |

---

## 10. High-risk pages (do NOT pilot with these; migrate late, with extra QA)
1. **Invoice Create / Edit** (all 5 doc-type variants) — money path, most plugins, most markup.
2. **Dashboard** — Chart.js init + grid.
3. **Login / LoginWith2fa** — separate layout + Turnstile + the auth critical path.
4. **InvoiceLists / InvoiceDetails2** — export/print + wide tables + QR.

---

## 11. Recommended migration order (phased)

- **Phase 1 — Audit (this document).** No code. → *approval gate*.
- **Phase 2 — Parallel scaffolding.** Add Tabler assets under `wwwroot/` (self-hosted). Create
  `_LayoutTabler.cshtml` + `_SidebarTabler.cshtml` beside the Velzon ones. Wire an **opt-in switch**
  (e.g. a page can set `Layout = "_LayoutTabler"`, or a config/claim flag) so nothing changes by default.
  Keep functional plugins (§4.1) loading identically. Velzon remains the default. `/ponytail-review` + run
  full Playwright.
- **Phase 3 — Pilot page.** Migrate ONE low-risk, well-covered page — **`Pages/Items/Index`**
  (has `06-crud-items` coverage, low complexity) — onto the Tabler layout. Validate visually + Playwright +
  responsive. → *approval gate*.
- **Phase 4 — Low-risk rollout.** Items (rest), Suppliers, PublicCustomer, Lead, Profile, Recurring.
- **Phase 5 — Admin area** (40 pages) in sub-batches (Codes, Users, Tools, Logs…).
- **Phase 6 — Medium/high**: Dashboard, InvoiceLists/Details, then **Invoices Create/Edit last**.
- **Phase 7 — Identity/auth** (`_LoginLayout` → Tabler) — login, 2FA, Manage.
- **Phase 8 — Retire Velzon**: switch default layout to Tabler, remove customizer + global-theme system
  (with agreement — §7), then a **separate** demo-bloat cleanup PR (§3.2).

Each phase: `/ponytail-review` → run all relevant Playwright + the invoice calc/permission tests → report.

---

## 12. Files that will be ADDED (Phase 2+)
- `wwwroot/tabler/css/tabler.min.css` (+ any Tabler add-ons), `wwwroot/tabler/js/tabler.min.js` (self-hosted)
- `wwwroot/assets/css/tabler-custom.css` (ported project overrides)
- `Pages/Shared/_LayoutTabler.cshtml`
- `Pages/Shared/_SidebarTabler.cshtml`
- `Pages/Shared/_LoginLayoutTabler.cshtml` (Phase 7)
- (optional) `wwwroot/assets/libs/remixicon/**` if we stop depending on the bundled `icons.min.css` for Remix

## 13. Files that will be MODIFIED (later phases, incrementally)
- `Pages/_ViewStart.cshtml` — only at the **final** cutover (switch default to Tabler)
- Individual `.cshtml` views — per-page, per-phase, class/markup only (no `.cshtml.cs` handler changes)
- `Program.cs` — only if/when CSP is tightened (optional, out of core scope)
- `CHANGELOG.md`, this audit — kept in sync each phase

## 14. Files that will EVENTUALLY be REMOVED (Phase 8, separate PRs)
- Velzon core CSS/JS: `assets/css/app.min.css`, `custom.min.css`, `assets/js/app.js`, `layout.js`,
  `plugins.js`, `app-baru.js`, `app1.js`, customizer images
- The theme-customizer offcanvas block in the (old) `_Layout.cshtml`
- `ThemeController` + `GlobalThemeService` + customizer JS (with agreement; DB table left intact)
- `assets/libs/*` demo HTML + unused plugin libraries (≈60 MB — separate cleanup PR)
- `wwwroot/*.html` scratch test files (`test-columns.html`, `theme-test.html`, …) if confirmed unused

## 15. Rollback strategy
- **Per phase:** every phase is a small PR merged only on green CI + passing Playwright. Revert = revert
  that PR (git). Because Velzon stays installed and the default layout is unchanged until Phase 8, any
  broken Tabler page can be reverted to Velzon by flipping that page's `Layout` back — **no data or
  backend impact**.
- **Cutover (Phase 8):** the default-layout switch in `_ViewStart` is a one-line revert. The global-theme
  retirement is additive (stop reading the table, don't drop it) → reversible.
- **No DB migration** is part of the theme swap → nothing to roll back at the data layer.

## 16. Test strategy
- **CI** (the compiler of record) must stay green each phase — no local .NET SDK here.
- **Playwright** (`tests/playwright`, run against staging with the Turnstile **test** key):
  after each phase run the relevant specs — `01-public`, `02-auth`, `03-authz`, `04-dashboards`,
  `05-responsive`, `06-crud-items`, `07-assets`, `08-crud-forms`, `09-2fa`. Coupling is low (tests key off
  form IDs + Select2/Flatpickr), so most pass unchanged — but responsive (375/768/1440) and `07-assets`
  (no external CDN) are the guards that catch Tabler regressions.
- **Functional guards after every phase:** create → submit an invoice (sandbox), confirm totals unchanged,
  generate one PDF, and confirm each role's menu + access. **Any** required change to a `.cshtml.cs`
  handler, calculation, LHDN call, or DB access = **STOP and report** (out of theme-swap scope).
- Add Tabler-specific visual checks to the pilot page before rollout.

---

## 17. Open decisions for approval (before Phase 2)
1. **Asset location** — `wwwroot/tabler/**` (clean separation, recommended) vs folding into `assets/`.
2. **Layout switch mechanism** — per-page `Layout =` opt-in (simplest, recommended for pilot) vs a
   config/claim feature flag for whole-area toggling.
3. **Global theme system** — confirm we may retire the customizer + `/api/Theme/*` at Phase 8 (§7). The
   customizer offcanvas is ~950 lines of Velzon demo UI; Tabler's theming is simpler and mostly static.
4. **Icon consolidation** — keep the bundled `icons.min.css` (Remix + tail) through migration; schedule a
   separate pass to converge on Remix (+ Tabler Icons where convenient) afterwards.

---

*End of Phase 1 audit. Awaiting approval to proceed to Phase 2 (parallel Tabler scaffolding, no default
change). No application code has been modified.*
