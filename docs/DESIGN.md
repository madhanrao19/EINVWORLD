# EINVWORLD Enterprise Design System
## Tabler-Based Enterprise SaaS Design System

Version: 1.1
Status: Source of Truth
Framework: Latest Project-Approved Tabler Version
Platform: ASP.NET Core Razor Pages
Target: Desktop • Tablet • Mobile

---

# PURPOSE

This document is the **single source of truth** for the visual design, UX, component architecture, responsive behaviour and implementation standards of the EINVWORLD platform.

It must be followed by all developers, designers, AI coding agents and contributors.

This document overrides previous UI conventions including the former Velzon implementation.

> **Superseded: colour palette.** The Navy/Blue/Teal palette in the COLOUR SYSTEM section below was
> never adopted — the team explicitly chose to keep the existing green "EinvWorld Professional" brand
> instead (see CHANGELOG 2026-07-15). The actual authoritative colour tokens are
> `wwwroot/tabler/css/einvworld-tokens.css` (`--einv-primary: #006948` etc.). Everything else in this
> document (components, spacing, responsive rules, patterns) is still the source of truth.

---

# PRODUCT OVERVIEW

EINVWORLD is a Malaysian enterprise SaaS platform for LHDN MyInvois compliant e-Invoicing.

The platform consists of:

- Public Website
- Authentication
- Supplier Portal
- Buyer Portal
- Administrator Portal
- Invoice Management
- Self-Billed Documents
- LHDN Integration
- Reporting
- AI Assistance
- Monitoring
- Audit
- Settings
- Billing
- Support

The system is used by finance teams and business users.

It is NOT a generic admin dashboard.

---

# DESIGN PHILOSOPHY

The product should feel similar in quality to:

- Stripe
- Xero
- HubSpot
- GitHub Enterprise
- Azure Portal
- Microsoft 365 Admin

DO NOT COPY THEM.

Use them only as references for quality.

The final product must feel uniquely EINVWORLD.

---

# IMPLEMENTATION FOUNDATION

Tabler is the implementation foundation.

Do NOT redesign Tabler.

Extend Tabler.

Reuse existing Tabler components wherever possible.

Never create custom components unless required by business requirements.

---

# DESIGN PRIORITY ORDER

Whenever requirements conflict, always follow this order.

1. Financial correctness
2. LHDN compliance
3. Security
4. Existing business functionality
5. Accessibility
6. Responsive usability
7. Design consistency
8. Visual polish

Visual appearance must NEVER break business functionality.

---

# MIGRATION PRINCIPLES

This project is a migration from Velzon to Tabler.

This is NOT a CSS conversion.

This is NOT a skin replacement.

Each page should be rebuilt using proper Tabler structure while preserving:

- Razor Pages
- Controllers
- Services
- ViewModels
- Validation
- Routing
- Authentication
- Authorization
- Invoice calculations
- LHDN integration
- Business logic

Do not preserve poor HTML merely because it already works.

---

# IMPLEMENTATION RULES

Always:

- Keep Tabler vendor files untouched.
- Store EINVWORLD variables separately.
- Use CSS variables where practical.
- Use SCSS if available.
- Reuse shared components.
- Reuse Razor partials.
- Reuse View Components.
- Reuse Tag Helpers.
- Remove duplicated CSS.
- Remove duplicated JS.
- Remove obsolete Velzon assets.
- Keep pages lightweight.

Never:

- Inline styles
- Page-specific component libraries
- Duplicate layouts
- Duplicate form logic
- Duplicate status rendering
- Duplicate navigation

---

# BRANDING

Use the supplied EINVWORLD logo exactly as provided.

Never:

- Stretch
- Compress
- Rotate
- Recolour
- Add text
- Add shadows
- Add outlines

Maintain clear space around the logo.

---

# COLOUR SYSTEM

> ⚠️ **Not implemented — superseded.** See the note near the top of this document. The live palette is
> `wwwroot/tabler/css/einvworld-tokens.css`, not the values below.

Primary Navy
#123B66

Primary Blue
#1769AA

Accent Blue
#2584D8

Teal
#00A6A6

Dark Teal
#087F8C

Background
#F7F9FC

Surface
#FFFFFF

Border
#DFE5EC

Text
#172033

Success
#15803D

Warning
#D97706

Danger
#C62828

Information
#2563EB

Green is ONLY for success.

Never use green as the primary brand colour.

---

# TYPOGRAPHY

Primary Font

Inter

Monospace

JetBrains Mono

Use clear hierarchy.

Never use tiny text.

Avoid excessive uppercase.

---

# SPACING

Use a strict 4px spacing system.

Allowed increments:

4
8
12
16
20
24
32
40
48
64
80

---

# BORDER RADIUS

Small
2px

Standard
4px

Medium
6px

Large
8px

XL
12px

Use subtle borders.

Avoid heavy shadows.

---

# APPLICATION SHELL

Desktop

- Left Sidebar
- Sticky Header
- Breadcrumb
- Page Header
- Content
- Notifications

Mobile

- Drawer Navigation
- Sticky Header
- Bottom Action Bar where appropriate

Supplier, Buyer and Admin share one shell.

Only permissions change.

---

# RESPONSIVE DESIGN

Support

Desktop

1440
1366
1280

Tablet

1024
768

Mobile

430
390
375

Never allow horizontal overflow.

---

# PAGE STRUCTURE

Every page contains:

- Breadcrumb
- Title
- Description
- Primary Action
- Secondary Actions
- Main Content

Never overload page headers.

---

# TABLES

Invoice tables are first-class components. Use the shared enterprise table pattern
(`einv-table-head` header + `einv-badge` status pills + `einv-mobile-stack` responsive
fallback — see `wwwroot/tabler/css/einvworld-tokens.css`) for every data table, not a
per-page reimplementation.

Support where applicable:

- Sticky header
- Comfortable density (default) and compact density option
- Column resizing where useful
- Column visibility controls
- Sort indicators
- Search
- Filter chips (show active filters as removable chips, not just a hidden filter panel)
- Bulk selection + a bulk action bar (appears only when rows are selected)
- Pagination
- Export
- Saved views
- Row actions
- Loading state
- Empty state (no data at all)
- No-results state (data exists, current filters matched nothing)
- Error state
- Keyboard accessibility (arrow/tab navigation, operable row actions)
- Responsive fallback

Row hierarchy: primary identifying column (e.g. Invoice No) reads clearly against
secondary/metadata columns — do not give every column equal visual weight.

Right-align:

- Currency
- Quantity
- Tax
- Percentages
- Totals

Do not put every row action as an always-visible button. Use one clear primary
inline action (e.g. View) and a row-actions menu (`einv-action-btn`) for everything
else. Never shrink financial tables until unreadable.

On smaller screens, choose one of:

- Horizontal scroll
- Sticky key column
- Priority columns (hide secondary columns progressively, per breakpoint)
- Stacked-card layout (`einv-mobile-stack`)
- Detail drawer

Do not automatically convert every table into cards below a fixed breakpoint —
choose the pattern that fits the table's column count and content.

---

# FORMS

Always use:

- Labels above controls (never rely on placeholders as labels)
- Logical grouping into sections (e.g. Buyer Details, Line Items, Totals — not one
  undifferentiated wall of fields)
- Helper text only where it adds information the label doesn't already convey —
  don't pad every field with boilerplate helper text
- Required-field indicators applied consistently across the whole app (same marker,
  same position, every form)
- Inline validation as the user completes a field
- An error summary at the top for long forms (invoice creation, company setup),
  linking down to each invalid field
- Preserved entered data after a validation error — never clear the form
- An unsaved-changes warning before navigating away with pending edits
- Autosave for long invoice drafts, with a visible save-state indicator
  (Saving… / Saved / Save failed)
- Searchable selects (Select2, already self-hosted) for long code lists — country,
  currency, tax type, classification, MSIC, unit-of-measure
- Code and description shown together in list options, e.g. `MYR — Malaysian Ringgit`,
  not the bare code alone
- Worked examples for Malaysian identifiers next to the field: TIN format
  (`C1234567890` / `IG12345678901`), Business Registration Number format, phone
  format (`+60…`)
- Sticky action bar for long forms so Save/Submit stays reachable while scrolling

Clearly distinguish, using a consistent visual convention across the app (not
one-off styling per page):

- Required
- Optional
- Conditional (only required given another field's value)
- Read-only
- Disabled
- System-generated
- Internal-only (never leaves EINVWORLD)
- Submitted to LHDN (the value is now part of a filed document — treat as
  effectively locked, and say so)

Do not hide important fields behind unexplained icons — an icon-only affordance for
something that changes what gets submitted to LHDN must carry a visible label or
persistent tooltip, not rely on discovery.

---

# BUTTONS

Primary

Blue

Secondary

Outline

Danger

Red

Only one dominant primary action per section.

---

# STATUS SYSTEM

Every status contains:

Text

Icon

Colour

Tooltip where useful

Never rely on colour alone.

---

# ACCESSIBILITY

Target WCAG 2.2 AA.

Support:

- High colour contrast (meet AA contrast ratios for text and meaningful UI, not
  just decorative elements)
- Visible keyboard focus on every interactive element — never `outline: none`
  without a replacement focus style
- Full keyboard navigation (tab order follows visual/reading order; every mouse
  action has a keyboard equivalent)
- Semantic headings (one `<h1>` per page, no skipped levels, headings describe
  actual structure — not chosen for font size)
- Proper `<label>`/`aria-label` on every form control, not placeholder-as-label
- Accessible validation (errors are programmatically associated with their field
  via `aria-describedby`, not conveyed by colour/icon alone)
- Screen-reader status announcements for async state changes (`aria-live` regions
  for toasts, save-state, submission results) — a sighted user seeing a toast
  appear must have a screen-reader equivalent
- Accessible modals (focus trapped inside while open, focus returns to the
  trigger on close, `Esc` closes, labelled via `aria-labelledby`)
- Skip-navigation link to jump past the sidebar/topbar to main content
- Minimum 44×44px touch targets on interactive controls where practical
- Reduced-motion support — honour `prefers-reduced-motion`, disable non-essential
  transitions/animations for users who request it
- Charts with a text summary/data-table alternative — a chart's information must
  not be locked inside a canvas/SVG only a sighted user can read
- Status conveyed by text + icon, never colour alone (see STATUS SYSTEM)

Do not rely on tooltips for essential information — a tooltip is a supplement,
never the only place a required fact appears. Icons must support, not replace,
understandable text.

---

# SECURITY UX

Never expose:

Passwords

Secrets

API Keys

Tokens

Mask sensitive values.

Require confirmations for destructive actions.

---

# LHDN UX

Clearly distinguish:

Save Draft

Submit

Validate

Retry

Cancel

Reject

Never imply approval is guaranteed.

---

# AI UX

AI may suggest.

AI never decides.

Always require user review.

Never silently modify invoices.

---

# COMPONENT LIBRARY

Create reusable components only — one implementation per component, shared across
Supplier, Buyer, and Admin. Never duplicate a component's markup per page, and
never build a second version of something Tabler or an already-self-hosted library
already provides.

Each entry below is tagged with its implementation source:

- **Tabler** — use the framework component as-is (or with `einvworld-tokens.css`
  brand overrides only). Do not rebuild it.
- **Lib: `<name>`** — an EINVWORLD-specific wrapper around a library already
  self-hosted in `wwwroot/assets/libs/` (see that library's existing usage before
  adding new markup).
- **Custom** — no existing Tabler component or self-hosted library covers this;
  it must be built as an EINVWORLD component, kept small and reused everywhere.
- **Gap** — not implemented anywhere in the app today and no library is
  self-hosted for it. Building it requires either a new FOSS library (per
  CLAUDE.md's FOSS-only dependency policy) or a small custom implementation —
  decide per the size of the actual need, don't add a dependency for one page.

### Actions
- **Buttons** — Tabler (`.btn-primary/-secondary/-outline/-danger`). One dominant
  primary action per section (see BUTTONS).
- **Button groups** — Tabler (`.btn-group`).
- **Icon buttons** — Tabler icon-only `.btn`/`.nav-link` pattern (see the theme
  toggle in `_TablerTopbar.cshtml` for a worked example). Always carries an
  accessible name (`aria-label` or `visually-hidden` text) — an icon alone is
  never a sufficient label.

### Text & selection inputs
- **Inputs** — Tabler `.form-control`.
- **Text areas** — Tabler `.form-control` (textarea).
- **Search boxes** — Custom (`app-search`, already themed in
  `einvworld-tokens.css`) — debounced, with a clear/close affordance.
- **Select menus** — Lib: Select2, themed `bootstrap-5`. Use for any list with
  more than ~8 options.
- **Multi-select** — Lib: Select2 (`multiple` mode) with code+description shown
  per selected chip.

### Date & time inputs
- **Date pickers** — Lib: Flatpickr.
- **Date-range pickers** — Lib: Flatpickr (`mode: "range"`).
- **Time pickers** — Lib: Flatpickr (`enableTime`).

### Specialized inputs
- **Currency input** — Lib: Cleave.js for input masking/grouping, paired with
  server-side decimal(18,2) validation — the mask is a UX aid, never the source
  of truth for precision (see CLAUDE.md decimal-precision rule).
- **Percentage input** — Lib: Cleave.js, decimal(18,6) rate precision server-side.
- **Phone input** — **Gap.** No international phone library is self-hosted today.
  Until one is added, use a plain `.form-control` with a Malaysian-format example
  in helper text (`+60 12-345 6789`) and server-side format validation. Don't add
  a phone-input library for a single field — only if a real multi-country need
  emerges.

### File inputs
- **File upload** — Lib: FilePond (already self-hosted) for single/simple
  uploads.
- **Drag-and-drop upload** — Lib: Dropzone.js (already self-hosted) — use where
  bulk/multi-file drag-drop is the actual interaction (e.g. CSV import,
  attachments), not as a default replacement for a plain file input.

### Choice controls
- **Checkboxes** — Tabler `.form-check`.
- **Radio buttons** — Tabler `.form-check` (radio).
- **Switches** — Tabler `.form-switch`.

### Navigation & disclosure
- **Tabs** — Tabler `.nav-tabs`, brand underline styling already in
  `einvworld-tokens.css`.
- **Accordions** — Tabler `.accordion`.
- **Breadcrumbs** — Tabler `.breadcrumb`.
- **Pagination** — Tabler `.pagination`, brand active-state styling already in
  `einvworld-tokens.css`.

### Data display
- **Tables** — Custom shared pattern on top of Tabler `.table` — see TABLES.
- **Editable tables** — Custom — inline-editable cells built on the same shared
  table pattern; edit affordance is explicit (not silently editable on click),
  and every edit follows the same validate → save → confirm flow as a form field.
- **Cards** — Tabler `.card`.
- **KPI cards** — Custom (`.card.einv-kpi-*`, 4px semantic left-accent, already
  in `einvworld-tokens.css`).
- **Charts** — Lib: Chart.js (already self-hosted). Every chart needs a text/data
  summary alternative (see ACCESSIBILITY).

### Status
- **Badges** — Tabler `.badge`, semantic tint/icon pairing already in
  `einvworld-tokens.css` (table-scoped) — never colour alone (see STATUS SYSTEM).
- **Status chips** — Custom (`.einv-badge-*`) for compact inline status pills
  outside table cells.

### Overlays
- **Tooltips** — Tabler/Bootstrap tooltip. Supplement only — never the sole
  carrier of essential information (see ACCESSIBILITY).
- **Popovers** — Tabler/Bootstrap popover.
- **Dropdown menus** — Tabler `.dropdown-menu`.
- **Modals** — Tabler `.modal`. Focus-trapped, labelled, closable via `Esc`
  (see ACCESSIBILITY).
- **Confirmation dialogs** — Lib: SweetAlert2 (already self-hosted, brand button
  colour applied globally in `_LayoutTabler.cshtml`). Required for destructive or
  regulated actions (see SECURITY UX, LHDN UX).
- **Side drawers** — Tabler `.offcanvas`.

### Feedback
- **Toasts** — Custom, built on the Bootstrap Toast component (`toast-success`
  host already in `_LayoutTabler.cshtml`, `window.einvworld.toast()` helper).
- **Banners** — Tabler `.alert` used page-wide/persistent (e.g. environment
  indicator — see SECURITY UX).
- **Alerts** — Tabler `.alert`.
- **Empty states** — Custom — explain why there's no data and what to do next
  (see EMPTY, ERROR & PERMISSION STATES pattern in `CLAUDE-UI-RULES.md`).
- **Skeleton loading states** — Tabler `.placeholder`/`.placeholder-glow`.

### Progress & sequence
- **Timelines** — Tabler `.timeline` (used for invoice status history/audit
  trail).
- **Step indicators** — Tabler `.steps`.
- **Progress bars** — Tabler `.progress`.

### Identity
- **Avatars** — Tabler `.avatar` + the `.avatar-xxs`…`.avatar-xl` size scale
  already in `einvworld-tokens.css`.
- **Organisation logos** — Custom — company/supplier logo display, falls back to
  initials avatar (`.einv-avatar-initials`) when no logo is set. Never stretch,
  recolour, or distort an uploaded logo.

### Utility
- **Command menu** — **Gap.** No command-palette library is self-hosted. Only
  build this if there's a concrete navigation/search need it solves that the
  existing topbar search doesn't — don't add it speculatively.
- **QR-code container** — Lib: qrcodejs (already self-hosted, used today on
  invoice PDF/detail pages for the LHDN validation QR). Reuse the existing
  pattern rather than a new implementation.
- **Code and JSON viewer** — **Gap.** No syntax-highlighting library is
  self-hosted. For the rare page that needs it (e.g. raw LHDN payload for
  support/debugging), a plain `<pre><code>` block with `overflow-wrap`/`overflow-x`
  handling (already in `einvworld-tokens.css`) is sufficient — don't add a
  highlighting library for occasional internal-only use.
- **Copy-to-clipboard control** — Custom — small icon-button wrapper around the
  Clipboard API, with a toast/tooltip confirming the copy succeeded (never silent
  — a screen-reader user needs the same confirmation).

## State variants

Not every component has every state — apply only the states that are meaningful
for that component:

- **Default, Hover, Focus, Active, Disabled** — apply to every interactive
  component (buttons, inputs, selects, tabs, links, menu items).
- **Loading** — applies to anything that triggers an async action: buttons,
  forms, tables, file uploads, search, command menu.
- **Success, Warning, Error** — apply where the component itself carries a
  validation or outcome state: inputs/text areas (validation), file upload
  (accepted/rejected), badges/status chips (business status), banners/alerts
  (message severity), buttons (rare — e.g. a save button briefly confirming
  success). They do not apply to purely structural components (breadcrumbs,
  avatars, pagination, tabs, dividers).

Focus state must always be visible (see ACCESSIBILITY) — do not implement a
component whose only visual difference from Default is colour, since that fails
users who can't perceive colour and users navigating by keyboard on a low-contrast
display.

---

# CONSISTENCY

Every page must reuse the same:

Typography

Spacing

Buttons

Tables

Forms

Cards

Navigation

Status badges

Icons

Dialogs

Notifications

No module should feel like another application.

---

# ACCEPTANCE CRITERIA

A page is considered complete only when:

✓ Matches the design system

✓ Uses Tabler components

✓ No Velzon styling remains

✓ No console errors

✓ No layout overflow

✓ Responsive

✓ Keyboard accessible

✓ Existing functionality preserved

✓ Existing tests pass

✓ Playwright desktop pass

✓ Playwright tablet pass

✓ Playwright mobile pass

✓ Dark mode supported (if enabled)

✓ Long company names render correctly

✓ Large invoice values render correctly

✓ Empty states implemented

✓ Error states implemented

✓ Loading states implemented

✓ Validation states implemented

---

# DEFINITION OF DONE

A migrated page is complete only when:

- Velzon HTML removed
- Velzon CSS removed
- Velzon JS removed
- Shared Tabler layout used
- Shared components used
- Responsive verified
- Accessibility verified
- Existing business logic preserved
- Playwright verified
- Security reviewed

---

# AI IMPLEMENTATION RULES

When implementing pages:

Do not create standalone HTML mockups.

Implement directly into the existing Razor Pages application.

Reuse existing models.

Reuse validation.

Reuse business logic.

Replace layout where necessary.

Never preserve poor HTML simply because it functions.

Design quality takes priority over preserving old markup.

Business functionality takes priority over visual polish.

---

# FINAL GOAL

The finished platform should feel like one professionally designed enterprise SaaS product.

It must never feel like:

- Tabler demo pages
- Velzon leftovers
- Multiple templates combined
- Separate applications

It should feel like EINVWORLD was designed from scratch using Tabler.