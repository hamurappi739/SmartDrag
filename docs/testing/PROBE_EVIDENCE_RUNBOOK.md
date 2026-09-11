# G1/G2 Probe Evidence Runbook

Use only after Gate G0 passes on a Windows machine with .NET 8. P0/G2 logs are diagnostic evidence, not production telemetry.

For an automated startup/shutdown smoke check (no drag interaction, therefore not G1/G2 evidence), append `--smoke`
to either probe command. Smoke mode exits after native window/OLE initialization and still emits a normal JSONL log.

## 1. Build gate

```powershell
./scripts/build.ps1
```

Stop if restore/build/tests fail. Do not compensate by editing architecture solely to make the probe compile.

## 2. G1 / P0 session

Run the repository wrapper so the latest mode-specific JSONL log is selected and a machine-readable
`*.summary.json` is retained beside it:

```powershell
./scripts/run-probe.ps1 -Mode p0
```

The wrapper invokes the probe, resolves the bundled/explicit Python interpreter, and runs the analyzer with
`--require-interaction`. A normal (non-smoke) run therefore fails closed when no drag/drop interaction was recorded.
If the machine does not expose `py -3`, set `SMARTDRAG_PYTHON` to a Python 3 executable before running the wrapper.

Optional non-interactive initialization check:

```powershell
./scripts/run-probe.ps1 -Mode p0 -Smoke
```

Smoke mode intentionally omits `--require-interaction`; it checks only startup/shutdown and is never G1 evidence.

Execute `docs/testing/P0_TEST_MATRIX.md`. Use a dedicated session/log. P0 intentionally relaxes pre-overlay payload knowledge and therefore cannot prove production qualification.

Analyze:

```powershell
python ./scripts/analyze_probe_log.py <path-to-jsonl> --json-out <path-to-summary.json> --require-interaction
```

Automatic red flags include focus theft, native-boundary failures, MOVE effects, dropped evidence, malformed JSONL, and
path-like privacy-sensitive fields. Manual matrix review is still mandatory.

## 3. G2 session

```powershell
./scripts/run-probe.ps1 -Mode g2
```

The equivalent startup/shutdown check is `./scripts/run-probe.ps1 -Mode g2 -Smoke`; it cannot prove qualification
behavior and intentionally does not require interaction.

Execute `docs/testing/G2_QUALIFICATION_TEST_MATRIX.md` using a separate log.

A clean G2 result requires, at minimum:

- no P0 signal-only warning;
- unsupported/ambiguous payloads suppressed before overlay;
- shown overlays have `Eligible`/`Supported` preflight evidence;
- actual `CF_HDROP` at `Drop` revalidates as authoritative;
- preflight and authoritative path identity match;
- COPY-or-NONE only; never MOVE;
- no focus change caused by overlay presentation;
- no native-boundary exceptions or dropped probe evidence.

## 4. Evidence retention

For a single end-to-end capture (launch, analyze, and package), use the PowerShell wrapper. It keeps the output
directory bounded below `artifacts/` and refuses to package a failed run:

```powershell
./scripts/capture-probe-evidence.ps1 -Mode g2 -Surface explorer-folder -Dpi "100%" -OutputDirectory ./artifacts/probe-evidence/<run-id>
```

Add `-Smoke` for startup/shutdown checks only. Smoke packages are useful for runtime diagnostics but intentionally
report `interactionEvidencePresent: false` and can never satisfy G1/G2.

Before manual review, verify the retained package independently:

```powershell
python ./scripts/verify_probe_evidence.py ./artifacts/probe-evidence/<run-id>
```

Add `--require-interaction` for a gate-review package. The verifier recomputes hashes, reparses JSONL, checks privacy,
and fails when manifest and analyzer summary disagree.

For each meaningful run, retain together:

- raw JSONL log;
- analyzer stdout;
- Windows version/build;
- .NET SDK/runtime version;
- Explorer surface tested (folder view/Desktop);
- monitor topology and scaling percentages;
- the exact git/manifest checkpoint hash.

Do not store unrelated user file paths or names in the evidence bundle. Use disposable test fixtures.

Package one run only after analysis succeeds:

```powershell
python ./scripts/package_probe_evidence.py <path-to-jsonl> --summary <path-to-summary.json> --out-dir ./artifacts/probe-evidence/<run-id> --surface explorer-folder --dpi "100%"
```

The package contains only `probe.jsonl`, `probe.summary.json`, and `manifest.json`. It refuses malformed JSONL,
path-like privacy fields, missing analyzer fields, or a non-empty destination directory. The manifest hashes both
retained files and records the git revision and operator metadata; it does not claim that manual verification passed.
After writing the package, the command automatically invokes `scripts/verify_probe_evidence.py`; a package is not
reported as created if the independent verifier rejects it.

## 5. Gate decision

Passing an analyzer is not enough. Update `PROJECT_STATE.md` and the relevant ADR/evidence docs only after the manual matrix has also been reviewed. If a surface cannot satisfy fail-closed qualification, explicitly exclude that surface or research a documented fallback; never silently downgrade production to P0 signal-only behavior.
