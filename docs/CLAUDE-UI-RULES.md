\# CLAUDE-UI-RULES.md



\## EINVWORLD Tabler UI Implementation Playbook



Version: 1.0

Status: Mandatory implementation rules

Applies to: Claude Code, Hermes, coding agents, developers, reviewers

Primary design authority: `DESIGN.md`

Application: EINVWORLD ASP.NET Core Razor Pages



\---



\# 1. PURPOSE



This document defines how UI changes must be implemented in the EINVWORLD codebase.



`DESIGN.md` defines what the product should look and behave like.



This document defines how to inspect, plan, implement, test, review, and hand over those changes safely.



These rules apply to:



\* Velzon-to-Tabler migration

\* New pages

\* Existing page redesigns

\* Responsive improvements

\* Shared component work

\* Public website changes

\* Authentication pages

\* Supplier pages

\* Buyer pages

\* Administrator pages

\* Invoice workflows

\* LHDN integration screens

\* Reporting

\* AI-assisted pages

\* Settings

\* Monitoring

\* Support

\* Error and system states



Do not treat UI work as a cosmetic CSS task.



UI changes must preserve all business behaviour, security boundaries, validation, calculations, data integrity, and LHDN functionality.



\---



\# 2. SOURCE-OF-TRUTH ORDER



When implementing UI work, use the following source-of-truth order:



1\. Existing business rules and verified application behaviour

2\. LHDN MyInvois requirements

3\. Security and permission boundaries

4\. `CLAUDE.md`

5\. `DESIGN.md`

6\. This `CLAUDE-UI-RULES.md`

7\. Approved Google Stitch or supplied page designs

8\. Existing Tabler implementation patterns

9\. Existing legacy markup



When requirements conflict, follow this priority:



1\. Financial correctness

2\. LHDN and legal compliance

3\. Security and data isolation

4\. Existing business functionality

5\. Accessibility

6\. Responsive usability

7\. Design-system consistency

8\. Visual fidelity

9\. Visual polish



Never sacrifice business correctness to match a design.



Never preserve poor legacy markup merely because it currently renders.



\---



\# 3. REQUIRED WORKFLOW



Use this order for every UI task:



1\. Inspect

2\. Roast

3\. Plan

4\. Implement

5\. Review

6\. Test

7\. Fix

8\. Verify

9\. Handoff



Do not skip any stage.



\---



\# 4. INSPECT



Before editing, inspect the existing implementation.



Read:



\* `CLAUDE.md`

\* `DESIGN.md`

\* This file

\* Relevant project documentation

\* Existing layout files

\* Shared partials

\* View components

\* Tag Helpers

\* Relevant Razor Pages

\* Page models

\* Controllers

\* Services

\* JavaScript

\* CSS or SCSS

\* Tests

\* Permission checks

\* Validation

\* Routing

\* Related database usage

\* LHDN integration dependencies



Identify:



\* Current page route

\* Page model

\* Bound properties

\* Form handlers

\* Validation behaviour

\* JavaScript behaviour

\* Existing AJAX or fetch calls

\* Table filtering

\* Sorting

\* Pagination

\* Export

\* Upload

\* Download

\* Modal behaviour

\* Permission checks

\* Role checks

\* Tenant or company isolation

\* Status rendering

\* LHDN actions

\* Existing shared components

\* Velzon-specific dependencies

\* Tabler components already available



Do not edit until the page behaviour is understood.



Do not assume a page is purely visual.



\---



\# 5. ROAST



Challenge the requested change before implementation.



Identify risks such as:



\* Business logic hidden inside Razor markup

\* JavaScript tied to existing element IDs

\* Validation tied to field names or DOM structure

\* Velzon plugins still required by the page

\* Duplicate layouts

\* Conflicting CSS

\* Shared components that could be affected

\* Role or permission regressions

\* Tenant isolation risks

\* Company context errors

\* LHDN submission risks

\* Invoice calculation risks

\* Mobile usability problems

\* Accessibility failures

\* Table overflow

\* Long-text clipping

\* Duplicate form submission

\* Unsaved form data

\* Destructive actions lacking confirmation

\* Third-party controls depending on legacy styles



State which parts can be safely redesigned and which parts must be preserved.



Do not proceed blindly from screenshots.



\---



\# 6. PLAN



Create a focused implementation plan before editing.



The plan should identify:



\* Files to change

\* Files not to change

\* Shared components to reuse

\* Shared components to create

\* Legacy elements to remove

\* Business behaviour to preserve

\* Responsive behaviour

\* Accessibility requirements

\* Tests to run

\* Playwright scenarios

\* Security review points

\* Database review requirements

\* LHDN safety considerations

\* Rollback or low-risk implementation approach



Keep the scope limited to the requested pages and directly related shared components.



Do not refactor unrelated modules.



Do not upgrade frameworks, packages, Tabler, Bootstrap, or dependencies unless explicitly required.



\---



\# 7. IMPLEMENTATION MODE



Unless explicitly stated otherwise, use:



`MODE: CODE IMPLEMENTATION`



For code implementation:



\* Implement directly in the existing ASP.NET Core application.

\* Do not create standalone HTML mockups.

\* Do not create disconnected demo pages.

\* Do not create a second application shell.

\* Do not recreate existing business logic.

\* Do not replace server-side functionality with static UI.

\* Do not remove features because they are absent from a mockup.

\* Do not change routes unless required.

\* Do not change database structure unless required.

\* Do not change LHDN behaviour unless explicitly approved.



Approved modes:



\* `MODE: DESIGN REVIEW`

\* `MODE: DESIGN ONLY`

\* `MODE: CODE IMPLEMENTATION`

\* `MODE: RESPONSIVE FIX`

\* `MODE: ACCESSIBILITY REVIEW`

\* `MODE: UI MIGRATION`

\* `MODE: VISUAL QA`



The active task should clearly state the mode.



\---



\# 8. TABLER IMPLEMENTATION RULES



Tabler is the UI foundation.



Use the project-approved and pinned Tabler version.



Do not independently upgrade Tabler during page migration.



Reuse Tabler components wherever suitable.



Examples:



\* Application shell

\* Sidebar

\* Header

\* Breadcrumbs

\* Cards

\* Forms

\* Buttons

\* Button groups

\* Tables

\* Badges

\* Alerts

\* Tabs

\* Dropdowns

\* Pagination

\* Modals

\* Drawers

\* Toasts

\* Tooltips

\* Skeletons

\* Empty states

\* Progress indicators

\* Avatars

\* Step indicators

\* Responsive navigation



Do not redesign Tabler from scratch.



Extend Tabler only where EINVWORLD business requirements need specialised behaviour.



Custom components must follow `DESIGN.md`.



\---



\# 9. VENDOR FILES



Never directly edit:



\* Tabler vendor CSS

\* Tabler vendor JavaScript

\* Bootstrap vendor files

\* Third-party package files

\* Generated package contents

\* Minified vendor assets



Keep EINVWORLD customisation separate.



Use:



\* CSS variables

\* SCSS variables

\* Shared component styles

\* EINVWORLD-specific stylesheets

\* Shared JavaScript modules

\* Razor partials

\* View components

\* Tag Helpers



This allows future Tabler upgrades without losing project changes.



\---



\# 10. VELZON MIGRATION RULES



A Velzon-to-Tabler migration is not complete merely because the page looks different.



Remove or replace Velzon-specific:



\* Layout markup

\* CSS classes

\* JavaScript

\* Plugins

\* Data attributes

\* Icons

\* Cards

\* Buttons

\* Navigation

\* Forms

\* Tables

\* Modals

\* Dropdowns

\* Page headers

\* Spacing utilities

\* Theme variables

\* Assets



Do not remove a legacy dependency until its usage has been verified.



Do not delete functional third-party styles merely because they originated in the old template.



Replace them safely first.



A page is considered migrated only when:



\* It uses the approved Tabler shell.

\* It uses shared EINVWORLD components.

\* Velzon-specific markup is removed.

\* Velzon-specific CSS is removed where no longer needed.

\* Velzon-specific JavaScript is removed or replaced.

\* No legacy visual conflicts remain.

\* Existing functionality works.

\* Responsive behaviour is verified.

\* Accessibility is verified.

\* Playwright scenarios pass.



\---



\# 11. PRESERVE BUSINESS LOGIC



Preserve:



\* Razor model binding

\* Form field names

\* Handler methods

\* Controllers

\* Services

\* Entity Framework patterns

\* Validation attributes

\* Server-side validation

\* Client-side validation

\* Routing

\* Query parameters

\* Permission checks

\* Role restrictions

\* Tenant isolation

\* Company isolation

\* TIN isolation

\* Invoice calculations

\* Tax calculations

\* Currency precision

\* Rounding rules

\* LHDN document behaviour

\* API calls

\* Webhooks

\* Audit logging

\* Background jobs

\* Exports

\* Imports

\* Uploads

\* Downloads

\* Existing security controls



Do not copy business logic into views.



Do not move business logic into JavaScript merely for convenience.



Do not duplicate calculations in the frontend unless the existing architecture already requires a preview calculation.



The server remains authoritative.



\---



\# 12. RAZOR PAGE RULES



Use semantic, maintainable Razor markup.



Preserve:



\* `asp-for`

\* `asp-page`

\* `asp-page-handler`

\* `asp-route-\*`

\* `asp-validation-for`

\* `asp-validation-summary`

\* Anti-forgery protection

\* Existing route values

\* Existing permission rendering



Do not replace Tag Helpers with hard-coded URLs.



Do not rename element IDs without checking dependent JavaScript and tests.



Do not rename fields without checking model binding.



Do not remove hidden inputs unless their purpose is verified.



Do not render sensitive data into hidden inputs unnecessarily.



Use partials or components for repeated UI.



\---



\# 13. SHARED COMPONENT RULES



Before creating a new component, check whether one already exists.



Prefer shared components for:



\* Page headers

\* Breadcrumbs

\* Status badges

\* Filter bars

\* Search controls

\* Data tables

\* Pagination

\* Empty states

\* Error states

\* Permission states

\* Loading skeletons

\* Confirmation dialogs

\* Invoice totals

\* Audit timelines

\* LHDN status panels

\* Form sections

\* Sticky action bars

\* Mobile action bars

\* Notification banners

\* Organisation context

\* Environment indicators



Do not duplicate component markup across pages.



Do not create multiple status badge systems.



Do not create different table systems for Supplier, Buyer, and Admin.



Supplier, Buyer, and Admin should share the same design language.



\---



\# 14. CSS RULES



Use shared CSS or SCSS.



Prefer:



\* CSS variables

\* Existing Tabler utilities

\* Bootstrap utilities

\* Shared component classes

\* Small, scoped extensions



Avoid:



\* Inline styles

\* `!important` unless strictly necessary

\* Deep selector nesting

\* Unscoped global overrides

\* Page-specific copies of shared styles

\* Magic numbers

\* Fixed heights for variable content

\* Absolute positioning for core layout

\* Widths that only work for one screen

\* Styling based on generated element IDs

\* Duplicated media queries



Any custom style should have a clear reason.



Remove obsolete styles after verifying that no other page uses them.



\---



\# 15. JAVASCRIPT RULES



Reuse existing JavaScript patterns.



Prefer:



\* Small modules

\* Event delegation where suitable

\* Progressive enhancement

\* Existing project helpers

\* Tabler or Bootstrap-supported behaviours



Avoid:



\* Global variables

\* Inline event handlers

\* Duplicated logic

\* DOM selectors tied to fragile visual structure

\* Recalculating authoritative financial values only in JavaScript

\* Silent failures

\* Multiple submit handlers

\* Loading the same library more than once



Prevent duplicate submission.



Disable or lock actions while processing.



Restore controls after recoverable errors.



Preserve form data after validation failures.



\---



\# 16. RESPONSIVE RULES



Verify at minimum:



\* 1440px

\* 1366px

\* 1280px

\* 1024px

\* 768px

\* 430px

\* 390px

\* 375px



Pages must not have accidental horizontal overflow.



Mobile requirements:



\* Sidebar becomes an off-canvas drawer.

\* Header remains compact.

\* Forms stack logically.

\* Important actions remain reachable.

\* Touch targets should be at least 44px where practical.

\* Text must not clip.

\* Buttons must not overflow.

\* Dropdowns and dialogs must fit.

\* Filters may move into a drawer.

\* Long identifiers must wrap or scroll safely.

\* Invoice workflows may use sticky bottom actions.

\* Important totals remain visible.

\* Financial values remain readable.



Do not simply shrink desktop layouts.



Do not hide essential financial or legal information for visual convenience.



\---



\# 17. TABLE RULES



Use the shared enterprise table pattern.



Support where applicable:



\* Search

\* Filters

\* Sorting

\* Pagination

\* Export

\* Bulk selection

\* Bulk actions

\* Row actions

\* Empty state

\* No-results state

\* Loading state

\* Error state

\* Responsive behaviour



Right-align:



\* Currency

\* Quantity

\* Tax

\* Percentages

\* Totals



Do not show every row action as a separate visible button.



Use one primary inline action where necessary and a menu for secondary actions.



On smaller screens, choose an appropriate pattern:



\* Horizontal scroll

\* Sticky key column

\* Priority columns

\* Expandable rows

\* Summary card

\* Detail drawer



Do not automatically convert every table into cards.



\---



\# 18. FORM RULES



Use:



\* Labels above controls

\* Clear required indicators

\* Logical sections

\* Inline validation

\* Validation summaries for long forms

\* Searchable selects for long lists

\* Code and description together

\* Smart defaults

\* Conditional fields

\* Unsaved-change warning

\* Save-state indication

\* Sticky actions for long workflows



Never use placeholders as the only label.



Clearly distinguish:



\* Required

\* Optional

\* Conditional

\* Read-only

\* Disabled

\* System-generated

\* Internal-only

\* Submitted to LHDN



Preserve user-entered values after errors.



Do not clear forms after failed submission.



\---



\# 19. INVOICE WORKFLOW RULES



Invoice UI changes must preserve:



\* Invoice types

\* Invoice numbering

\* Buyer details

\* Supplier details

\* Line items

\* Quantity

\* Unit

\* Unit price

\* Discounts

\* Charges

\* Taxes

\* Tax exemption details

\* Subtotals

\* Totals

\* Rounding

\* Payment details

\* References

\* Attachments

\* Notes

\* Validation

\* Draft behaviour

\* Template behaviour

\* Submission behaviour

\* Status history



Do not change calculation precision.



Do not introduce client-side rounding differences.



Do not submit invoices to real LHDN during UI testing.



Use safe test, sandbox, mocked, or non-submission scenarios unless specifically approved.



\---



\# 20. LHDN UX RULES



Clearly distinguish:



\* Save locally

\* Save as draft

\* Internal approval

\* Queue submission

\* Submit to LHDN

\* Await validation

\* Validated

\* Invalid

\* Retry

\* Request cancellation

\* Request rejection

\* Synchronise



Never imply:



\* Approval is guaranteed.

\* A submitted invoice can always be edited.

\* A validated invoice can always be cancelled.

\* A rejection request is automatically accepted.

\* EINVWORLD is an official LHDN product.



Before real submission, show:



\* Environment

\* Organisation

\* Invoice number

\* Document type

\* Buyer

\* Total

\* Submission action



Use explicit confirmation for regulated external actions.



\---



\# 21. ROLE AND PERMISSION RULES



Preserve distinctions between:



\* Platform Administrator

\* Supplier Administrator

\* Supplier User

\* Buyer Administrator

\* Buyer User

\* Reviewer

\* Approver

\* Support roles

\* Any existing project roles



Only authorised Supplier users may create ordinary supplier invoices.



Buyer users receive invoices and may perform permitted review or rejection-request actions.



Do not expose actions merely by hiding buttons.



Server-side authorisation remains mandatory.



The UI should explain unavailable actions where useful.



Never weaken permissions to simplify UI implementation.



\---



\# 22. TENANT AND COMPANY ISOLATION



Preserve:



\* Tenant boundaries

\* Company boundaries

\* Organisation context

\* TIN boundaries

\* User-company membership

\* Role scope

\* Environment scope



Always show the active organisation where confusion could cause a financial or submission error.



Do not allow UI state from one company to appear under another company.



Do not trust client-supplied company identifiers without server validation.



\---



\# 23. SECURITY UX RULES



Never expose:



\* Passwords

\* API secrets

\* Access tokens

\* Refresh tokens

\* Private keys

\* Full bank details where masking is required

\* Internal stack traces

\* Sensitive configuration

\* Production identifiers unnecessarily



Use confirmation for:



\* Deletion

\* Suspension

\* Revocation

\* Credential rotation

\* Environment changes

\* LHDN submission

\* Cancellation

\* Destructive bulk actions



Display persistent environment indicators:



\* Production

\* Staging

\* Sandbox

\* Test



Production and non-production must be visually unmistakable.



\---



\# 24. ACCESSIBILITY RULES



Target WCAG 2.2 AA.



Verify:



\* Semantic headings

\* Form labels

\* Keyboard operation

\* Focus visibility

\* Logical focus order

\* Screen-reader labels

\* Accessible validation

\* Accessible tables

\* Accessible modals

\* Accessible drawers

\* Skip navigation

\* Reduced motion

\* Status announcements

\* Alternative text

\* No colour-only meaning

\* No keyboard traps

\* Meaningful links

\* Sufficient contrast

\* Touch-target size



Do not rely on tooltips for essential information.



Icons must support, not replace, understandable text.



\---



\# 25. CONTENT RULES



Use clear Malaysian business English.



Preferred terms:



\* Supplier

\* Buyer

\* e-Invoice

\* LHDN MyInvois

\* TIN

\* Registration Number

\* Invoice Number

\* Submission Status

\* Validation Status

\* Self-Billed e-Invoice

\* Credit Note

\* Debit Note

\* Refund Note



Do not create a separate Customer concept when Buyer is intended.



Use specific actions:



\* Save as Draft

\* Submit to LHDN

\* Retry Submission

\* Request Rejection

\* Create Credit Note

\* Download Validated Invoice

\* Invite User

\* Test Connection



Avoid vague labels:



\* Proceed

\* Execute

\* Do Action

\* Continue Process

\* Confirm Operation



\---



\# 26. ICON RULES



Use Tabler Icons unless the existing approved design system specifies otherwise.



Do not mix icon libraries.



Use the same icon for the same action across the platform.



Icons must:



\* Align with text

\* Use consistent sizing

\* Use consistent stroke weight

\* Have accessible labels where required

\* Remain understandable

\* Not replace essential text



\---



\# 27. LOADING AND PROCESSING STATES



Every asynchronous action must show an understandable state.



Use:



\* Loading indicators

\* Skeletons

\* Disabled duplicate actions

\* Progress messages

\* Processing labels

\* Completion confirmation

\* Recoverable error messages



For long LHDN operations, explain that processing is ongoing.



Do not let users repeatedly submit because nothing appears to happen.



Do not show false success before the server confirms success.



\---



\# 28. EMPTY, ERROR, AND PERMISSION STATES



Empty states should explain:



\* Why there is no data

\* What users can do next

\* Which action is available



No-results states should show:



\* Current search or filters

\* Clear filters action

\* Search guidance



Error states should explain:



\* What happened

\* Whether data was saved

\* Whether retry is safe

\* What action to take next

\* Correlation ID where available



Permission states should explain:



\* Which action is unavailable

\* Why it may be restricted

\* Which role or permission is required

\* Who may assist



Never show internal exception text to normal users.



\---



\# 29. DESIGN FIDELITY RULES



Approved designs are the visual source of truth for:



\* Layout

\* Spacing

\* Hierarchy

\* Component placement

\* Responsive intent

\* Visual density

\* Page structure



However, designs may omit:



\* Validation

\* Permissions

\* Loading states

\* Error states

\* Existing actions

\* Technical constraints

\* Business rules

\* Edge cases



Do not remove working features merely because they are missing from the screenshot.



Adapt the approved design to include all required functionality.



Do not merely add Tabler classes to the old layout.



Rebuild the HTML structure where necessary.



\---



\# 30. REALISTIC DATA TESTING



Test with realistic edge cases:



\* Long company names

\* Long buyer names

\* Long invoice numbers

\* Long UUIDs

\* Long TIN values

\* Large currency values

\* Negative values where supported

\* Zero values

\* Many line items

\* Long item descriptions

\* Multiple tax rows

\* No records

\* One record

\* Hundreds of records

\* Validation errors

\* Permission restrictions

\* API unavailable

\* LHDN invalid response

\* Slow processing

\* Mobile keyboard open

\* Narrow mobile viewport



Do not test only with short demo content.



\---



\# 31. PLAYWRIGHT QA



Run Playwright for representative desktop, tablet, and mobile viewports.



Minimum viewports:



\* Desktop: 1440 × 900

\* Tablet landscape: 1024 × 768

\* Tablet portrait: 768 × 1024

\* Mobile: 390 × 844

\* Small mobile: 375 × 667



Verify:



\* Page loads

\* No console errors

\* No failed critical network requests

\* No horizontal overflow

\* Navigation works

\* Mobile drawer works

\* Forms submit

\* Validation displays

\* Filters work

\* Sorting works

\* Pagination works

\* Row actions work

\* Dropdowns work

\* Modals work

\* Drawers work

\* Loading states appear

\* Error states are usable

\* Permission states are correct

\* Keyboard focus is visible

\* Existing business behaviour remains intact



Capture screenshots for changed pages.



Compare screenshots with the approved design.



Fix meaningful differences before handoff.



\---



\# 32. TESTING RULES



Run all relevant tests after implementation.



Depending on the change, run:



\* Build

\* Unit tests

\* Integration tests

\* Razor or UI tests

\* JavaScript tests

\* Playwright tests

\* Accessibility checks

\* Formatting or linting

\* Static analysis



Do not claim success if tests were not run.



Do not say “production ready” unless all required checks pass.



If a test cannot be run, state exactly why.



\---



\# 33. SECURITY REVIEW



Use the security reviewer for relevant changes.



Review:



\* Permission checks

\* Sensitive data exposure

\* Cross-tenant access

\* Hidden-field trust

\* Unsafe URLs

\* XSS risks

\* CSRF protection

\* File upload controls

\* Download authorisation

\* Destructive actions

\* Environment confusion

\* LHDN submission safety

\* Secret exposure

\* Error messages

\* Audit logging



Fix critical or high-risk findings before handoff.



\---



\# 34. DATABASE REVIEW



Use the DBA reviewer if the task touches:



\* Entity Framework models

\* Migrations

\* Queries

\* Database schema

\* Indexes

\* Stored procedures

\* Data seeding

\* Data conversion

\* Pagination query logic

\* Reporting queries



Do not change the database merely to simplify a UI implementation.



Avoid loading excessive data into memory for tables.



Use server-side pagination for large datasets.



\---



\# 35. PERFORMANCE RULES



Avoid:



\* Duplicate CSS

\* Duplicate JavaScript

\* Loading unused libraries

\* Loading full datasets for paginated views

\* Unoptimised large images

\* Excessive DOM size

\* Re-rendering entire tables unnecessarily

\* Multiple identical API calls

\* Blocking page load for non-critical widgets



Prefer:



\* Shared bundles

\* Lazy loading where suitable

\* Server-side pagination

\* Efficient queries

\* Debounced searches

\* Optimised assets

\* Minimal custom JavaScript



UI redesign must not significantly degrade page performance.



\---



\# 36. SCOPE CONTROL



Do not:



\* Refactor unrelated files

\* Rename unrelated components

\* Change unrelated routes

\* Upgrade unrelated packages

\* Reformat the entire repository

\* Replace working architecture

\* Create speculative abstractions

\* Introduce a new frontend framework

\* Convert Razor Pages to another architecture

\* Modify real LHDN submission logic without approval

\* Commit secrets

\* Delete legacy files before verifying they are unused



Implement the smallest coherent and production-safe change that fully satisfies the design.



“Smallest safe change” does not mean retaining unsuitable legacy markup.



Rebuild the page structure when necessary, but avoid unrelated refactoring.



\---



\# 37. ACCEPTANCE CRITERIA



A UI task is complete only when:



\* `DESIGN.md` is followed.

\* The approved Tabler foundation is used.

\* Existing functionality is preserved.

\* Existing permissions are preserved.

\* Tenant and company isolation are preserved.

\* Invoice calculations are unchanged.

\* LHDN behaviour is unchanged unless approved.

\* No unwanted Velzon styling remains.

\* No unnecessary legacy dependency remains.

\* No browser console errors exist.

\* No accidental horizontal overflow exists.

\* Desktop behaviour is verified.

\* Tablet behaviour is verified.

\* Mobile behaviour is verified.

\* Keyboard operation is verified.

\* Validation is verified.

\* Loading states are verified.

\* Empty states are verified.

\* Error states are verified.

\* Permission states are verified where relevant.

\* Existing automated tests pass.

\* Playwright scenarios pass.

\* Security review is complete.

\* DBA review is complete if applicable.

\* Relevant screenshots are captured.

\* Changed files are documented.



\---



\# 38. DEFINITION OF DONE



A page or module is done only when:



\* The new design is implemented in the real application.

\* It uses the shared Tabler application shell.

\* It uses reusable EINVWORLD components.

\* Legacy Velzon markup is removed.

\* Legacy conflicting CSS is removed.

\* Legacy JavaScript is removed or replaced.

\* Business logic remains intact.

\* Server-side validation remains intact.

\* Client-side validation remains intact.

\* Role and permission checks remain intact.

\* Responsive behaviour is verified.

\* Accessibility is verified.

\* Playwright tests pass.

\* Relevant automated tests pass.

\* Security issues are resolved.

\* Database concerns are resolved if applicable.

\* No known critical regression remains.



A screenshot alone is not proof of completion.



A successful build alone is not proof of completion.



A visually attractive page with broken functionality is not complete.



\---



\# 39. HANDOFF FORMAT



At completion, provide:



\## Summary



Describe what was changed.



\## Files changed



List all modified, added, and removed files.



\## Components



List shared Tabler or EINVWORLD components reused or created.



\## Preserved behaviour



Confirm the business functionality that remains unchanged.



\## Responsive verification



Report desktop, tablet, and mobile checks.



\## Test results



List commands and results.



\## Playwright results



List tested pages, viewports, and scenarios.



\## Security review



Summarise findings and fixes.



\## Database review



State whether database code was touched and the review result.



\## Velzon removal



List removed Velzon dependencies, classes, scripts, styles, or assets.



\## Remaining issues



List unresolved issues honestly.



\## Screenshots



Provide before-and-after or final screenshots where available.



Do not say the task is production ready if any required verification failed or was not performed.



\---



\# 40. STANDARD TASK PROMPT



Use this structure when assigning a UI task:



```text

Use ponytail full.



Read:

\- CLAUDE.md

\- DESIGN.md

\- CLAUDE-UI-RULES.md

\- Relevant project documentation



MODE: UI MIGRATION



Task:

\[Describe the exact pages or module.]



Approved design references:

\[List supplied screenshots, ZIP files, Stitch designs, or references.]



Requirements:

\- Treat the approved design as the source of truth for layout, spacing, hierarchy, and visual design.

\- Preserve all business logic.

\- Preserve Razor binding.

\- Preserve validation.

\- Preserve routing.

\- Preserve permissions.

\- Preserve tenant and company isolation.

\- Preserve invoice calculations.

\- Preserve LHDN behaviour.

\- Replace legacy markup where necessary.

\- Do not merely apply Tabler classes to old Velzon HTML.

\- Reuse shared Tabler and EINVWORLD components.

\- Remove obsolete Velzon styling from migrated pages.

\- Do not refactor unrelated files.

\- Do not modify real LHDN submission behaviour.

\- Do not commit secrets.



Follow this workflow:

1\. Inspect

2\. Roast

3\. Plan

4\. Implement

5\. Security review

6\. DBA review if database code is touched

7\. Playwright QA

8\. Fix defects

9\. Verify

10\. Handoff



Verify at:

\- 1440px

\- 1024px

\- 768px

\- 390px

\- 375px



Do not stop after visual changes.



Verify both appearance and functionality.

```



\---



\# 41. FINAL RULE



The objective is not to make EINVWORLD resemble a Tabler demo.



The objective is to build a coherent, responsive, accessible, secure, and maintainable EINVWORLD enterprise product using Tabler as the foundation.



Every completed page must look and behave like part of the same application.



No Velzon leftovers.



No disconnected mockups.



No broken business logic.



No unverified claims.



No skipped testing.



