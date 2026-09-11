# Preview history clear phase

Status: closed.

The Preview history card now has an explicit, reversible-scope clear action. It hides only operations from the local
history view; generated files and source files are never touched. The clear boundary is persisted alongside the bounded
history document and restored at startup, so old in-memory queue snapshots cannot reappear after a restart.

## Delivered

- added a version-tolerant history document envelope with `Entries` and optional `ClearedAt`;
- retained loading support for the previous JSON-array history format;
- added a localized `Clear history` / `Очистить историю` action with a clear no-file-deletion confirmation;
- persisted the clear boundary and applied it to both live queue snapshots and stored history;
- wired the action into theme, language, keyboard focus, tooltip, and automation-name updates;
- added round-trip and legacy-format regression coverage.

## Verification

- `dotnet build .\SmartDrag.sln -c Debug --no-restore --verbosity minimal`: PASS, 19 projects, 0 warnings, 0 errors;
- `dotnet test .\SmartDrag.sln -c Debug --no-build --no-restore --verbosity minimal`: PASS, 165 tests, 0 failures;
- `scripts/validate_repo.py --allow-build-artifacts`: PASS;
- preview self-check: PASS, 21/21 checks.

The clear action is history-only. It does not broaden output deletion authority and does not alter G1/G2/G3 or native
activation gates.
