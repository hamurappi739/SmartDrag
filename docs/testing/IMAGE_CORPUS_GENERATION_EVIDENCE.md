# Image Corpus Generation Evidence — v0.6

Generated locally with Pillow 12.3.0 using `tools/generate_image_corpus.py`.

Observed post-generation facts:

- `exif-orientation-6.jpg`: JPEG 320×240, 1 frame, EXIF Orientation = 6;
- `icc-srgb.jpg`: JPEG 320×240, 1 frame, embedded ICC profile present;
- `png-text-metadata.png`: PNG 320×240 with `Author` and `Comment` text fields;
- `alpha-gradient.png`: PNG 128×128 RGBA;
- `animated-negative.gif`: GIF 32×32, 2 frames;
- malformed fixtures are intentionally preserved as raw invalid/truncated bytes.

`tests/fixtures/images/CORPUS_MANIFEST.json` records deterministic SHA-256 and byte size for all nine fixtures. `scripts/validate_image_corpus.py` verifies those bytes without requiring an image codec.

The codec-neutral container audit is also part of the build gate:

```powershell
python ./scripts/audit_image_corpus.py --json-out ./artifacts/g3-corpus/audit-latest.json
```

It records signatures, dimensions, frame counts, EXIF/ICC/text metadata signals, and intentional negative fixtures
without decoding or encoding through a production library. The report is preparation evidence only; its
`codecDecision` remains `UNRESOLVED` until a concrete WebP candidate passes the full G3 matrix.

This evidence validates the fixture generator only. It is not G3 codec evidence.
