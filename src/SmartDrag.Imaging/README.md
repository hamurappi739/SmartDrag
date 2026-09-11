# SmartDrag.Imaging

Contracts only during the Windows proof phase.

Do **not** add ImageSharp, SkiaSharp, WIC wrappers, or another codec dependency until P0 Windows drag/overlay proof is complete and the image-processing license/behavior decision is recorded.

Open product decisions before implementation:

- compress quality policy per source format;
- PNG strategy (lossless vs quantization);
- static vs animated input support;
- WebP lossy/lossless policy;
- alpha handling;
- EXIF/XMP/IPTC removal policy;
- EXIF Orientation normalization before metadata removal;
- ICC profile policy.
