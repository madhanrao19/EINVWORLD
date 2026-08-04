# eInvWorld AI Workspace — future-state UX reference

**Status: reference-only material, kept for future consideration. The AI Workspace itself is NOT
approved for implementation and is not scheduled — only keeping this folder as a design reference
is approved.**

## What's here

- `DESIGN.md` — Stitch-generated design tokens (colors, type, spacing, elevation) for a proposed
  "AI Finance Workspace" — a 4-pane review UI (session nav / chat copilot / document workspace /
  context pane) with inline field-level AI suggestions, confidence scores, and a collaborative
  audit timeline.
- `mockup.html` — the static HTML mockup. **Do not serve, link, or wire this up as-is**: it loads
  Tailwind and Google Fonts from a CDN (this app is self-hosted, no CDN — see `CLAUDE.md`), and
  every "AI" behavior in it (suggestions, confidence %, submission-readiness simulation, evidence
  trail) is hardcoded fake data with no backend behind it.
- `screen.png` — rendered screenshot of the mockup.

## Why it's parked here instead of built

Assessed against `CLAUDE.md` and the current codebase: this is a multi-phase product initiative
(new data model for suggestion/accept/reject/undo state, a much heavier UI shell, live validation
wiring, provenance/evidence tracking, collaboration workflow) — not a UI restyle ticket. It does not
have an approved scope, budget, or phased plan yet.

See [`IMPLEMENTATION-PROPOSAL.md`](./IMPLEMENTATION-PROPOSAL.md) for the phased build-out proposal.
**No phase in that proposal may be started without explicit review and approval** — this reference
folder and the proposal are planning artifacts, not a green light.

## Origin

Dropped as a Stitch export at `stitch_einvworld_tabler_redesign/` (outside the repo) and copied
here 2026-08-04 for durability. First referenced in `CHANGELOG.md` under
*"2026-08-03 — Restyle: finish rolling the 'EinvWorld Professional' tokens onto Create/Edit Invoice"*,
where the same not-in-scope decision was made for that restyle pass.
