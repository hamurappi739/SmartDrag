# Preview diagnostics UX phase

Status: closed.

The local self-check is now a complete user-facing diagnostic flow. After the 21-check verification finishes,
Preview keeps the report location, shows a clear pass/fail result, and offers a safe button to open the report folder.
The report path is never copied into a command surface or used to bypass runtime/output authorities.

## Delivered

- added an Open report folder action next to Run self-check;
- retained the latest report path only for the current window session;
- made the report-folder action available after both pass and fail when a report was written;
- localized the button, tooltip, automation name, success status, and failure status;
- removed the transient report action when local preview data is reset;
- used shell folder opening only for the diagnostic artifact, with safe exception handling.

## Verification

- dotnet build SmartDrag.sln: PASS, 19 projects, 0 warnings, 0 errors;
- dotnet test SmartDrag.sln: PASS, 186 tests, 0 failures;
- scripts/validate_repo.py --allow-build-artifacts: PASS;
- preview self-check: PASS, 21/21 checks.

This phase does not expose private source/output paths and does not modify native activation or G1/G2/G3 gates.
