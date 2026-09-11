# Preview dynamic diagnostics phase

Status: closed.

The Preview self-check result now reflects the report that was actually produced instead of a hard-coded check count. The presentation layer reads only a bounded aggregate from the JSON report:

- passed checks and total checks;
- aggregate pass/fail state, including partial failures;
- elapsed duration when present;
- localized Russian/English result copy.

The parser is fail-closed, case-insensitive for the report contract, limited to 64 KiB, and does not expose check names, technical details, or absolute paths to the result label. Malformed, inaccessible, oversized, or out-of-contract reports keep the existing safe fallback text and report-folder action.

## Verification

- `dotnet test tests/SmartDrag.Presentation.Tests/SmartDrag.Presentation.Tests.csproj -c Debug --no-restore`: PASS, 33 tests;
- full solution build: PASS, all 19 projects;
- full solution tests: PASS, 189 tests, 0 failures;
- repository validator: PASS;
- headless Preview self-check: PASS, 21 checks.

The phase adds no native activation and does not widen deletion, codec, or completion authority.
