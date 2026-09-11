# Preview launcher phase

Status: closed.

The repository now includes `scripts/run-preview.ps1` as the canonical Preview entry point:

- it configures the repository-local .NET CLI/NuGet directories;
- it builds with one MSBuild worker by default, avoiding transient parallel-worker failures;
- it launches the WPF Preview with the correct `--preview` argument;
- `-SelfCheck` runs the same headless pipeline and writes a bounded report;
- `-NoBuild` supports fast relaunches after an already verified build;
- command failures propagate as non-zero errors instead of leaving an ambiguous terminal state.

Production activation remains gated and is not changed by the launcher.

## Verification

- launcher self-check: PASS;
- full solution Debug build: PASS, 19 projects, 0 errors (external NuGet advisory-feed warning `NU1900` only);
- full sequential xUnit suite: PASS, 230 tests, 0 failures;
- static repository validator: PASS;
- headless preview self-check: PASS, 21 checks;
- `git diff --check`: PASS.

