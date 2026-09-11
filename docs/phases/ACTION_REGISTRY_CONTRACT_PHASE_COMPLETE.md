# Action registry contract phase — complete

Date: 2026-09-04

## Outcome

Action definitions now fail fast at registry composition instead of failing later in overlay rendering or dispatch.
Every registered action must declare a stable id, user-facing display name, supported input category, and icon id.
Capability filtering remains centralized in `GetAvailable`, including the pre-G3 WebP hold.

## Implemented

- added composition-time validation for action metadata;
- rejected blank display names, icon ids, and empty supported-input sets;
- retained duplicate-id rejection and deterministic display-name ordering;
- added three regression tests for malformed definitions;
- preserved capability-aware action availability and no WebP exposure before G3.

## Verification

- `SmartDrag.Actions.Tests`: 8 passed, 0 failed;
- static validation remains green;
- no new runtime authority or capability was introduced.

## Scope boundary

This closes action-definition contract hardening. It does not select a codec, enable native activation, or replace the
authoritative orchestration commit gate.
