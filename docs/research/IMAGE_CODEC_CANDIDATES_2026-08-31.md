# Image Codec Candidate Research — 2026-08-31

This note records current package/licensing facts only. It does **not** activate a dependency or select a winner.

## SkiaSharp

Current stable package observed on NuGet on 2026-08-31: `SkiaSharp 4.151.1`, last updated 2026-08-05. NuGet identifies the package license as MIT.

The corresponding `SkiaSharp.NativeAssets.Win32 4.151.1` package is also MIT and its NuGet download package is substantially larger than the managed package, so publish/runtime footprint must be measured in G3 rather than inferred from the top-level package alone.

Potential strengths to measure:

- permissive MIT license;
- mature Skia engine;
- WebP support in the Skia ecosystem;
- established .NET bindings.

Risks/costs to measure:

- native runtime assets and self-contained publish size;
- metadata editing/round-trip ergonomics;
- ICC behavior;
- cancellation behavior around native calls;
- security/update cadence of native codec surface.

## SixLabors.ImageSharp

Current stable package observed on NuGet on 2026-08-31: `SixLabors.ImageSharp 4.1.1`, last updated 2026-08-20, targeting .NET 8+.

Six Labors states that ImageSharp 4 is the first major version to enforce a build-time license key for direct package dependencies. Their current licensing material describes eligibility/licensing conditions and warns not to commit license material to public repositories.

Potential strengths to measure:

- fully managed .NET deployment model;
- image processing APIs;
- metadata/color-management capabilities;
- small top-level NuGet download compared with native-asset stacks (publish output still must be measured).

Risks/costs:

- licensing/build-key workflow for direct ImageSharp 4 dependencies;
- commercial-use conditions must be evaluated against the eventual SmartDrag business/distribution model;
- CI/reproducible-build workflow must be proven without committing license secrets.

## Current decision

Neither package is accepted. G3 will implement equivalent spikes against `tests/fixtures/images/` and compare correctness, output size, memory, speed, cancellation, packaging, metadata/ICC/orientation behavior, and licensing workflow.

## Public sources checked

- NuGet: https://www.nuget.org/packages/SkiaSharp/
- NuGet: https://www.nuget.org/packages/SkiaSharp.NativeAssets.Win32
- NuGet: https://www.nuget.org/packages/SixLabors.ImageSharp/
- Six Labors ImageSharp 4 announcement: https://sixlabors.com/posts/announcing-imagesharp-400/
- Six Labors license enforcement note: https://sixlabors.com/posts/licence-enforcement-changes/
