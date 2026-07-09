Verify the EINVWORLD change before calling it complete.



Check:

1\. Build expectation

2\. Unit tests

3\. Integration tests if database touched

4\. Migration safety

5\. Login/logout

6\. Admin/Supplier/Buyer permissions

7\. Invoice create/edit/view/delete

8\. Invoice submission safety

9\. LHDN token handling

10\. Background jobs

11\. Logs and correlation IDs

12\. Secrets not exposed

13\. IIS deployment impact

14\. Playwright QA

15\. Documentation updated if needed



Return:

\- What was checked

\- What passed

\- What failed

\- Bugs found

\- Bugs fixed

\- Remaining risks

\- Final pass/fail



Never say production ready unless verification passes.

