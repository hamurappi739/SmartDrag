# Preview theme token phase

Status: closed.

The Preview visual layer now has one toolkit-neutral palette for both appearances:

- Light and Dark expose the same 15 named tokens;
- Apple-style accent, surface, separator, text, status, drop-hover, and cancellation colors are centralized;
- the WPF adapter resolves brushes from the active palette during every theme switch;
- progress, drop-zone, history, result, warning, error, and cancellation surfaces follow the active theme;
- primary text contrast is covered by automated checks;
- no runtime authority or action availability is changed by theme selection.

The existing code-built WPF controls still receive their initial construction defaults before `ApplyTheme`; the active
palette is applied before the window is shown and remains the source of truth for runtime appearance.

## Verification

- full solution Debug build: PASS, 19 projects, 0 errors (external NuGet advisory-feed warning `NU1900` only);
- full solution tests: PASS, 210 tests, 0 failures;
- static repository validator: PASS;
- headless preview self-check: PASS, 21 checks;
- `git diff --check`: PASS.
