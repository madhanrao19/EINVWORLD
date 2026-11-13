# Production Data Protection key rotation — plan (NOT approved, NOT executed)

Status: **proposed**. Nothing here has been done on Production. Each run needs the owner's explicit approval
for that date. Prepared 2026-11-13.

## Why

Verified on 2026-11-13 (read-only, from the server's key folder):

- Production's key ring (`E:\EINVWORLD\Keys`) holds two keys, `31fbed07` (current, expires 2027-01-29) and
  `d8ee5a3e` (expired 2026-11-02, still needed to read older values). Both are **plaintext** XML files.
- Both keys were also in Staging's key ring until 2026-11-13, when Staging was moved to its own key and the copies
  were removed. Local `Users` can read the Production key folder.
- Anyone who copied `31fbed07` can forge a Production sign-in cookie until that key leaves Production's ring.
  There is no evidence that it was copied; this rotation removes the possibility.

## What depends on the keys

| Use | Effect of removing the old keys |
|---|---|
| Sign-in, session, antiforgery and TempData cookies; 2FA "remember this device" | Every user is signed out once and signs in again. A form opened before the change fails once; reloading fixes it. |
| Password-reset (2 h) and email-confirmation (1 day) links | Links issued before the change stop working; the user asks for a new one. |
| Bank account numbers, address lines 2/3, webhook secrets (database) | **Unreadable unless re-keyed first** (step 5). |
| LHDN access tokens (database, 1 h lifetime) | Not re-keyed. A token stored under an old key would be sent to LHDN as garbage until it expires, so the old keys are removed only after every stored token has been renewed (step 6). |
| CSV import confirmation (2 h) | An import that is mid-confirmation must be uploaded again. |

Not affected: the LHDN signing certificate (`.p12`) is not used by Data Protection, and signing is off.

## Prerequisites

1. PR #487 (re-key in "Encrypt existing PII", v1.50.53) merged with green CI and deployed to Production.
2. `DataProtection:ProtectKeysWithDpapi` is not set to `false` on Production (default `true`), so the new key is
   written encrypted with machine DPAPI. Consequence: the key file works only on this server. A server rebuild then
   needs the database re-keyed before the move, or the values re-entered; record this in the DR notes.
3. A quiet window (evening or weekend) and a notice to users that they will be signed out.

## Steps

1. **Backups.** `BACKUP DATABASE [EINVWORLD] ... WITH COPY_ONLY, CHECKSUM`; copy `E:\EINVWORLD\Keys` to a folder
   only Administrators can read; copy `web.config`.
2. **Stop** the Production app pool.
3. **Retire the current key for new writes:** in `key-31fbed07….xml`, set `<expirationDate>` to yesterday (the key
   stays in the ring, so everything remains readable).
4. **Start** the pool. On first use the app creates a new key. Check: a third `key-*.xml` exists, contains
   `encryptedSecret` (not `masterKey`), health is 200, sign-in works.
5. **Re-key:** Admin → System Health → "Encrypt existing PII". Expected: `re-keyed N`, `0 unreadable`. Run it a
   second time: `0 re-keyed, 0 unreadable`. A red result means stop and go to Rollback.
6. **Wait at least 2 hours**, then confirm no stored LHDN token is still under an old key (count of
   `LHDNTokens.AccessToken` values whose key id is not the new key must be 0; tokens renew within the hour).
7. **Stop** the pool, **move** `key-31fbed07….xml` and `key-d8ee5a3e….xml` out of the ring into the admin-only
   backup folder, **start** the pool.
8. **Verify:** health 200; sign in (with 2FA); open a company with a bank account and an invoice PDF; run step 5
   once more (`0 re-keyed, 0 unreadable`); watch the next scheduled LHDN import; no "key was not found in the key
   ring" errors other than old cookies in the first hours.
9. Remove local `Users` read access from `E:\EINVWORLD\Keys` (as done on Staging), then delete the moved key files
   and the pre-rotation key backup once the owner confirms the rotation is final. The pre-rotation database
   backup is readable only with the old keys; keep both or neither.

## Rollback

- Before step 7: nothing is lost. To undo step 3, restore the original `key-31fbed07….xml` from the backup and
  recycle the pool; values re-keyed in step 5 stay readable because the new key is still in the ring.
- After step 7: stop the pool, copy the two old key files back into `E:\EINVWORLD\Keys`, start the pool. Old
  cookies and links work again.
- Never delete the new key once step 5 has run: the re-keyed values depend on it.

## Rehearsal evidence (Staging, 2026-11-13, a copy of Production data)

The same sequence was run on Staging: the copied key `31fbed07` was expired by its file, Staging created its own
DPAPI-encrypted key, 127 of 127 values were re-keyed with 0 unreadable, the four old keys were removed, a second
run reported 0 re-keyed and 0 unreadable, health stayed 200, and the next scheduled LHDN import completed with no
errors. Old session and antiforgery cookies were rejected as expected (one failed form post, fixed by a reload).
`PiiBackfillClassifyTests` covers old-key, current-key and unreadable ciphertext against a real file key ring.

Not rehearsed: 2FA sign-in and password reset after the change, a stored LHDN token under an old key (Staging's
tokens were not in encrypted form), and webhook secrets (Staging has none).

## Open points for the owner

- The number of Production values to re-key is unknown until step 5 reports it (the Production database is not
  queried from the development PC).
- Whether to treat the certificate password as exposed: it sat in Staging's `web.config` and its backups, which
  local users could read until 2026-11-13. Signing is off, so there is no live use today.
