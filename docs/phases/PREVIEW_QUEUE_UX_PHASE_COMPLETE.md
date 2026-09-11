# Preview queue UX phase

Status: closed.

The Preview now explains the sequential runtime queue instead of showing only a generic spinner. While an operation
is active, the status line identifies the safe display name, reports whether the job is waiting, running, or
cancelling, and tells the user how many additional jobs are queued behind it.

## Delivered

- projected the existing queued-behind count into the visible status line;
- localized queue count and operation state text in Russian and English;
- kept cancellation visibility tied to the runtime's CanCancel authority;
- added a presentation regression proving queued jobs are counted without leaking private parent paths;
- preserved the existing sequential execution and completion FIFO behavior.

## Verification

- dotnet build SmartDrag.sln: PASS, 19 projects, 0 warnings, 0 errors;
- dotnet test SmartDrag.sln: PASS, 186 tests, 0 failures;
- scripts/validate_repo.py --allow-build-artifacts: PASS;
- preview self-check: PASS, 21/21 checks.

This phase is presentation-only. It does not change queue authority, output policy, native activation, or G1/G2/G3
gate status.
