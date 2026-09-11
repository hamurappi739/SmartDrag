# Preview Capability Gating — Phase Complete

Status: CLOSED for unavailable-action gating  
Closed: 2026-09-03  
Branch: main

## Objective

Keep the preview's action surface truthful: a button must not look executable when its concrete implementation is still
blocked by an unresolved production gate.

## Delivered

- `AppCapabilities.WebpEncodingAvailable` is an explicit opt-in capability and defaults to `false`;
- `ActionRegistry` excludes the WebP action until that capability is true;
- preview and deterministic production composition explicitly pass `WebpEncodingAvailable=false`;
- the guarded WIC adapter still retains its controlled WebP rejection as a defense-in-depth boundary;
- registry tests cover both the hidden-by-default and enabled-capability paths.

## Verification record

- full solution build: PASS, 19 projects, 0 warnings, 0 errors;
- full solution tests: 139 passed, 0 failed, 0 skipped;
- preview self-check: 21/21 passed;
- static repository validation: PASS.

## Explicit boundary

This phase does not select a WebP codec or close G3. It only prevents the current UI/composition from advertising an
encoder that has not passed the G3 benchmark and acceptance matrix.
