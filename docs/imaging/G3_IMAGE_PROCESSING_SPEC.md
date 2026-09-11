# G3 — Image Processing Product & Safety Specification

## Status

Architecture/policy defined; concrete codec dependency **not accepted**. Build/Windows gates remain ahead of shipping integration.

## Preview implementation (not a G3 acceptance)

The repository now contains `SmartDrag.Windows.Imaging.WindowsWicImageCodec` for the WPF `--preview` host. It uses
Windows Imaging Component for content inspection and JPEG/PNG encode/remove-metadata operations, with a cheap JPEG
end-marker check for truncated input. This adapter is intentionally preview-only: WebP encoding is reported as a
controlled unavailable capability, numeric limits remain provisional, and no production activation or G3 decision
is implied.

## MVP source formats

For G3 evaluation, content inspection must recognize static JPEG and PNG. Extension is not authoritative. GIF, WebP input, AVIF, HEIC, TIFF, SVG and multi-frame formats remain outside the first image-input slice unless a later ADR expands scope.

## Global invariants for all three actions

- local-only processing;
- never overwrite/delete source;
- preserve visible orientation;
- preserve pixel dimensions unless orientation normalization requires swapping width/height;
- preserve alpha where the target format supports it;
- preserve ICC/color-management information when representable;
- reject animated/multi-frame input in MVP;
- inspect content before full decode;
- enforce configured source-size/dimension/decoded-pixel limits;
- write only to SmartDrag-owned temporary output until successful validation/commit;
- cancellation cleans the temporary artifact;
- malformed/truncated input returns a controlled error, never a process crash.

## Compress

### JPEG

Intent: meaningful size reduction with visually reasonable quality while keeping dimensions unchanged.

G3 benchmark must choose quality/subsampling/effort based on corpus evidence; no magic quality number is canonical yet. Non-visual metadata is preserved by the action unless the selected codec makes a specific metadata class impossible to round-trip; such loss must be documented before acceptance.

### PNG

Intent: lossless recompression/optimization. Compress must not silently turn PNG into JPEG/WebP; format conversion belongs to a separate action.

If optimization produces a larger artifact, G3 must define whether to treat this as `NoBenefit` and discard the generated file. This remains open pending implementation measurements.

## Convert to WebP

- output extension is `.webp`;
- input is static JPEG/PNG in the first slice;
- alpha must be preserved for PNG input;
- visible orientation must be preserved;
- ICC must be preserved when supported by the chosen codec/container path;
- non-visual metadata should be preserved when reliably representable; unsupported metadata loss must be documented;
- animated conversion is out of scope;
- exact lossy/lossless selection and quality/effort are G3 benchmark decisions.

## Remove Metadata

Canonical behavior is ADR-025:

- remove non-visual EXIF/XMP/IPTC/textual metadata where possible;
- normalize orientation into pixels before removing orientation metadata when necessary;
- preserve ICC/color profile;
- preserve visible alpha/color/orientation;
- do not resize.

## Execution inspection

A codec adapter must implement `IImageInspector`. `MvpImageExecutionGuard` validates inspection facts against `ImageSafetyLimits` before full processing.

Numeric safety limits are deliberately not embedded into the library layer. G3 chooses values based on real peak-memory/runtime measurements on representative Windows hardware.

## Candidate codec research snapshot — 2026-08-31

- SkiaSharp NuGet stable observed: `4.151.1`, MIT licensed. The Win32 native-assets package is large enough that packaging footprint must be measured, not assumed.
- ImageSharp 4 is .NET 8+ and offers managed codecs/metadata/color-management APIs, but direct ImageSharp 4 dependencies use Six Labors' build-time license-key enforcement. Licensing/build workflow therefore remains a material selection criterion.

These facts do not select a winner. G3 requires an implementation spike using the same corpus and measurements.

## G3 measurements

For each candidate implementation capture:

- correctness for every corpus fixture/action;
- output bytes;
- size ratio;
- encode/decode elapsed time;
- peak working set (or closest reliable Windows measurement);
- cancellation responsiveness;
- package/publish size;
- native dependency count;
- metadata round-trip facts;
- ICC/orientation result;
- malformed-input behavior;
- license/build/package implications.

## v0.8 mandatory guard wiring

The pure `MvpImageExecutionGuard` is now enforced by `GuardedImageProcessor`.

Production composition is required to wire:

```text
IImageInspector + ImageSafetyLimits
        -> GuardedImageProcessor
        -> concrete codec IImageProcessor
        -> action handlers
```

Action handlers must never receive the raw concrete codec directly. A future codec implementation is therefore unable to bypass format/frame/dimension/source-size/decoded-pixel checks merely through an incorrect composition shortcut.

The numeric limits remain intentionally unresolved until G3 benchmarks run on real Windows hardware.
