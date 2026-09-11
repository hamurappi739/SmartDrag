# Preview dispatch handoff phase

Status: closed.

The Preview now fails closed during the handoff between action dispatch and the first runtime snapshot:

- accepting an action keeps input controls busy until the queue publishes its operation snapshot;
- the first operation snapshot records the JobId before recalculating picker/drop availability;
- quiet snapshots clear the handoff busy flag and restore the ready state;
- picker and drop cannot slip through the small asynchronous gap between commit and presentation;
- runtime queue and capability authority remain unchanged.

## Verification

- full solution Debug build: PASS, 19 projects, 0 errors (external NuGet advisory-feed warning `NU1900` only);
- Presentation tests: PASS, 69 tests, 0 failures;
- static repository validator: PASS;
- headless preview self-check: PASS, 21 checks;
- `git diff --check`: PASS.

