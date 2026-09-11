# MVP Settings Policy — v0.9

## Current user settings

The current domain model retains only settings already justified by the product:

- drag activation delay;
- minimum drag travel distance;
- overlay cursor offset;
- preserve-source flag.

## Engineering guardrails

`MvpSettingsPolicy` rejects values outside these guardrails:

| Setting | Allowed |
| --- | ---: |
| ActivationDelayMs | 80–1200 ms |
| MinimumTravelDip | 4–96 DIP |
| CursorOffsetDip | 12–160 DIP |
| PreserveSource | must be `true` |

These ranges are safety/engineering bounds, **not** claims that the final tuned defaults are empirically optimal.
The defaults remain 180 ms, 12 DIP, and 28 DIP until Windows usability evidence changes them.

## Output policy

Production UI does not provide an `OutputPolicy` during commit.

`MvpOutputPolicyFactory` produces exactly:

```text
LocationMode    = SameDirectory
CollisionPolicy = GenerateUniqueName
PreserveSource  = true
```

A future configured output location must be introduced through the policy factory and an ADR rather than by exposing
arbitrary `OutputPolicy` construction to the view/native adapter.
