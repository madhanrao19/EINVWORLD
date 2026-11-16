# CodeQL alert dispositions

Open **high**-severity code-scanning alerts that are deliberately **not** code-fixed, with the reason and the
evidence. Reviewed for the v1.50.73 Production readiness assessment (2026-11-16, main `0a55444`). None of them is
new: all were opened in July or October 2026 and none was introduced by v1.50.69–v1.50.73.

Re-check an alert whenever the code it points at changes. To close one in GitHub, dismiss it with the reason below
(owner approval first).

| Alert | Rule | Location | Disposition |
|---|---|---|---|
| #344 | `cs/web/missing-token-validation` | `Controllers/ThemeController.cs` `SaveGlobalTheme` | **Not exploitable (false positive).** Admin-only (`[Authorize(Roles = "Admin")]` plus an explicit role check). It binds `[FromBody]` JSON, so the request must be `Content-Type: application/json`, which a cross-site form cannot send and `fetch` cannot send cross-origin without a CORS preflight (no CORS policy is configured). The auth cookie is `SameSite=Lax` (`Program.cs`), so it is not sent on a cross-site POST anyway. Changes only the global theme colours. |
| #335 | `cs/web/missing-token-validation` | `Controllers/CspReportController.cs` | **By design.** Anonymous endpoint that browsers POST CSP violation reports to; there is no session state to forge and nothing to protect with a token. It is rate limited (`public-forms` policy), swallows malformed reports, and logs each distinct violation once as structured fields (directive, blocked URI, document, source). |
| #334 | `cs/web/missing-token-validation` | `Controllers/Api/InvoiceImportApiController.cs` | **By design.** Machine-to-machine API authenticated with the `X-Api-Key` header (constant-time comparison, `CryptographicOperations.FixedTimeEquals`), not cookies, so CSRF does not apply: a browser cannot add that header cross-site. `[AllowAnonymous]` only bypasses cookie auth; the key check runs first. Disabled when `Api:Key` is empty. Validates only; creates or submits nothing. |
| #298 | `cs/web/xss` | `Pages/Invoices/InvoiceLists.cshtml:409` | **False positive.** The flagged value (`invoiceStatus`, from the query string) is used only as an `asp-route-*` value, which the anchor tag helper URL-encodes and then HTML-attribute-encodes, and it is first checked against the fixed `validStatuses` allowlist (anything else becomes `null`). CSP is enforced as a second layer. |
| #297 | `cs/web/xss` | `Pages/Invoices/InvoiceLists.cshtml:408` | **False positive.** Same as #298 (the `asp-route-LHDNStatus` value on the line above). |
| #86 | `cs/web/missing-function-level-access-control` | `Controllers/ResourcesApiController.cs` `GetEditorImage` | **By design.** Serves images embedded in the public Resources (marketing) articles, so it is anonymous on purpose (`[AllowAnonymous]`, GET only, no state change). The file name goes through `SafePath.TryResolve` (no path traversal outside `EditorUploadsFolder`). Image extensions get their image type, anything else `application/octet-stream`, and every response carries `X-Content-Type-Options: nosniff` and a sandboxing `Content-Security-Policy` (`default-src 'none'; ... sandbox`), so even an SVG can't run script. |

Medium-severity alerts (about 228 at the time of review) are almost all `cs/log-forging` and
`cs/exposure-of-sensitive-information` on structured Serilog calls and outgoing emails. Serilog writes
message-template properties as structured values, and TINs are masked with `LogSanitizer.MaskTin`. They were
triaged as noise in the November 2026 audit and are not listed one by one.
