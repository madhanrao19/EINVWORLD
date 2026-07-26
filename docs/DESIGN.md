# EINVWORLD Enterprise Design System
## Tabler-Based Enterprise SaaS Design System

Version: 1.0
Status: Source of Truth
Framework: Latest Project-Approved Tabler Version
Platform: ASP.NET Core Razor Pages
Target: Desktop • Tablet • Mobile

---

# PURPOSE

This document is the **single source of truth** for the visual design, UX, component architecture, responsive behaviour and implementation standards of the EINVWORLD platform.

It must be followed by all developers, designers, AI coding agents and contributors.

This document overrides previous UI conventions including the former Velzon implementation.

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

Invoice tables are first-class components.

Support:

- Search
- Filters
- Sorting
- Pagination
- Export
- Bulk Actions
- Sticky Header
- Responsive

Never shrink financial tables until unreadable.

---

# FORMS

Always use:

Labels above controls

Grouped sections

Validation

Autosave for long forms

Sticky action bar

Required indicators

Never rely on placeholders as labels.

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

Target WCAG 2.2 AA

Support:

Keyboard

Screen readers

Focus states

Reduced motion

Proper labels

Minimum 44px touch targets

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

Create reusable components only.

Examples:

Page Header

Summary Card

Data Table

Filter Bar

Search Box

Invoice Totals

Status Badge

Alert

Drawer

Modal

Timeline

Audit Card

Statistics Card

Loading Skeleton

Empty State

Error State

Permission State

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