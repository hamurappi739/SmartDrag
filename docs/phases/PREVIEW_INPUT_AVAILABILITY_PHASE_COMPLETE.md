# Preview input availability phase

Status: closed.

The Preview now makes input availability visible instead of merely rejecting invalid clicks in the handler:

- the header button and the large `+` button share one idle-state policy;
- both controls disable while a picker is open, a file is being inspected, a drag session is active, or an operation is
  running;
- the custom Apple-style button templates render a consistent disabled opacity and keep the focus treatment intact;
- accessibility help text explains whether the picker is ready or why the current operation must finish first;
- closing the window cancels in-flight inspection/presentation work through a lifetime token, preventing late callbacks from
  touching a closing dispatcher;
- cancellation during shutdown is silent and does not turn into a false user-facing inspection failure.

The change is presentation-only: queue authority, output safety, capability gates, and native activation policy are
unchanged.

## Verification

- full solution Debug build: PASS, 19 projects, 0 errors (external NuGet advisory-feed warning `NU1900` only);
- Presentation tests: PASS, 57 tests, 0 failures;
- static repository validator: PASS;
- headless preview self-check: PASS, 21 checks;
- `git diff --check`: PASS.

