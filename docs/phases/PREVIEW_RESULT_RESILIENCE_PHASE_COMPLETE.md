# Preview result resilience phase

Status: closed.

The result surface now fails closed when a generated path is stale or the output has disappeared outside SmartDrag.
The preview no longer renders copy/open/delete actions for a missing object, and history marks a live unavailable
result without exposing an absolute path. The history store also keeps its clear boundary when callers use the
legacy save overload.

## Delivered

- added output-availability tracking to privacy-safe history entries;
- added a localized “Result unavailable” state in the completion surface;
- hid stale result actions before they can present a misleading path;
- preserved the persisted history clear boundary across ordinary history writes;
- retained backward-compatible loading for the previous history array format;
- added regression coverage for availability, clear-boundary preservation, and legacy migration.

## Verification

- dotnet build SmartDrag.sln: PASS, 19 projects, 0 warnings, 0 errors;
- dotnet test SmartDrag.sln: PASS, 166 tests, 0 failures;
- scripts/validate_repo.py --allow-build-artifacts: PASS;
- preview self-check: PASS, 21/21 checks.

This is presentation hardening only. Runtime command authority, identity-backed deletion, G1/G2/G3, and native
activation gates are unchanged.
