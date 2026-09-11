# G3 image guard input-boundary phase — complete

Date: 2026-09-04

## Outcome

The mandatory `GuardedImageProcessor` boundary now rejects invalid processing paths before inspection or codec work.
Source and temporary output paths must be present and resolve to different objects, preventing a direct caller from
turning a processing request into an in-place overwrite.

## Implemented

- normalized source and temporary paths before codec authorization;
- rejected missing/blank paths with a safe `InvalidInput` result;
- rejected source/temporary path equality with `InvalidOutputPolicy`;
- preserved inspection, format, animation, dimension, pixel, and source-size gates;
- added regressions proving invalid requests never reach the inspector or inner codec.

## Verification

- `SmartDrag.Imaging.Tests`: 17 passed, 0 failed;
- full solution build remains green with the guard change;
- no concrete codec or WebP capability was enabled.

## Scope boundary

This closes deterministic input-boundary hardening only. G3 still requires codec selection, corpus benchmarking, and
numeric resource-limit acceptance before production image capabilities expand.
