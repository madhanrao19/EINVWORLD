/*
  Remove user memberships of LHDN general-TIN placeholder parties (v1.47.11).

  Why: a UserCompanies row linking a user to a general-TIN party (General Public EI00000000010, Foreign
  Buyer EI00000000020, Foreign Supplier EI00000000030, Government EI00000000040) made that user match
  EVERY tenant's invoices issued to that party. v1.47.11 ignores these TINs in the access checks and
  refuses new assignments; this script removes rows created before that (e.g. the demo supplier seeded
  with PartyInfoId 2).

  Safe to run more than once. Take a database backup first. Run step 1, review, then step 2.
*/

-- Step 1: preview
SELECT uc.UserId, u.Email, uc.PartyInfoId, p.CompanyName, p.TIN, uc.IsPrimaryCompany
FROM UserCompanies uc
JOIN PartyInfos p ON p.PartyInfoId = uc.PartyInfoId
LEFT JOIN AspNetUsers u ON u.Id = uc.UserId
WHERE p.TIN IN ('EI00000000010', 'EI00000000020', 'EI00000000030', 'EI00000000040');

-- Step 2: delete (uncomment to run)
-- BEGIN TRANSACTION;
-- DELETE uc
-- FROM UserCompanies uc
-- JOIN PartyInfos p ON p.PartyInfoId = uc.PartyInfoId
-- WHERE p.TIN IN ('EI00000000010', 'EI00000000020', 'EI00000000030', 'EI00000000040');
-- COMMIT;
