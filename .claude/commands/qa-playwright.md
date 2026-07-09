Run full EINVWORLD website QA using Playwright like a real QA tester.



Test public pages:

1\. Home

2\. About

3\. Contact

4\. Privacy

5\. Terms

6\. Resources/articles

7\. Search

8\. Login

9\. Forgot password



Test authenticated flows:

1\. Admin dashboard

2\. Supplier dashboard

3\. Buyer dashboard

4\. Company profile

5\. Party info

6\. Invoice list

7\. Invoice create/edit/view

8\. Templates

9\. Upload/download

10\. PDF generation

11\. Email/send actions if safe

12\. LHDN submission only in safe sandbox/local mode

13\. Logs/system pages if available



Security checks:

1\. Restricted URLs blocked

2\. Role menus correct

3\. Supplier cannot access another supplier data

4\. Buyer cannot access supplier admin pages

5\. Admin-only pages blocked for normal users

6\. CSRF/form validation

7\. File upload path traversal

8\. Console/network errors



Responsive checks:

1\. Desktop

2\. Tablet

3\. Mobile



If bugs are found:

1\. Capture issue

2\. Identify root cause

3\. Fix safely

4\. Re-run failed test

5\. Re-run regression checks



Final report:

\- Roles tested

\- Pages tested

\- Bugs found

\- Bugs fixed

\- Remaining bugs

\- Screenshots/traces

\- Final pass/fail

