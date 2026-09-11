# Preview Capability Hint — Phase Complete

Status: CLOSED for capability transparency  
Closed: 2026-09-03  
Branch: main

## Objective

Make the current action surface self-explanatory after capability gating: the preview must tell the user what can be
done now and why a gated operation is absent.

## Delivered

- the Safety & Scope card now includes a localized capability hint;
- the hint lists the two actions available in the current preview and explains that WebP is held for G3 validation;
- the text is theme-aware, uses a readable warning accent, and has an automation name for assistive tooling;
- the message is generated from `AppCapabilities.WebpEncodingAvailable`, so it remains truthful if a future accepted
  encoder enables the capability.

## Verification record

- Debug app build: PASS with 0 warnings and 0 errors;
- preview self-check: 21/21 passed;
- static repository validation: PASS;
- full solution suite remains green at 139 tests.

## Explicit boundary

This is a clarity improvement only. It does not enable WebP, select a codec, or change the G3 acceptance requirement.
