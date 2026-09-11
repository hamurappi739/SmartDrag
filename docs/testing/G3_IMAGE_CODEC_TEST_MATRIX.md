# G3 — Image Codec Acceptance Matrix

A codec candidate is not accepted because it can encode one PNG. Run the same matrix against every candidate.

## Functional

- inspect JPEG by content;
- inspect PNG by content;
- reject extension/content mismatch;
- reject truncated input cleanly;
- reject multi-frame/out-of-scope input;
- Compress JPEG;
- Compress PNG without format conversion;
- Convert JPEG → WebP;
- Convert alpha PNG → WebP with alpha;
- Remove Metadata from JPEG with EXIF orientation;
- Remove textual PNG metadata;
- preserve ICC where representable.

## Safety

- source bytes unchanged after success;
- source bytes unchanged after codec exception;
- source bytes unchanged after cancellation;
- partial file removed after failure/cancel;
- content dimensions guard executes before full processing where candidate APIs permit;
- configured source/dimension/pixel limits fail closed;
- no exception escapes into UI/native boundary.

## Performance/packaging

Measure on at least small, normal camera-size, large, alpha PNG, and metadata-heavy images:

- elapsed inspection time;
- elapsed processing time;
- peak memory/working set;
- output/source ratio;
- cancellation latency;
- self-contained publish size;
- native DLL/runtime assets;
- cold-start impact.

## Licensing/reproducibility

- license identified and archived in research note;
- CI/build prerequisites documented;
- fresh checkout restore/build behavior documented;
- redistribution requirements documented;
- no secret license material committed.

No candidate may be marked Accepted before G0 build works and these results are captured.
