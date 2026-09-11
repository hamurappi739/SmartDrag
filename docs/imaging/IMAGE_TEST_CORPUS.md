# G3 Image Test Corpus

Fixtures live in `tests/fixtures/images/` and are generated deterministically by `tools/generate_image_corpus.py` (Pillow; development-only dependency).

| Fixture | Purpose |
|---|---|
| `rgb-photo.jpg` | ordinary static photographic-style JPEG |
| `rgb-basic.png` | ordinary RGB PNG |
| `exif-orientation-6.jpg` | proves visible orientation is preserved and Remove Metadata normalizes before stripping orientation |
| `png-text-metadata.png` | proves non-visual textual metadata removal |
| `alpha-gradient.png` | proves alpha preservation, especially WebP conversion |
| `icc-srgb.jpg` | proves ICC/profile behavior |
| `animated-negative.gif` | negative/out-of-scope multi-frame/format case |
| `not-an-image.png` | extension/content mismatch |
| `truncated.jpg` | malformed/truncated decoder handling |

## Required G3 assertions

### Compress

- output decodes successfully;
- same visual orientation;
- expected dimensions;
- JPEG/PNG output remains same format;
- source remains byte-for-byte untouched;
- no committed partial on failure/cancellation.

### Convert to WebP

- output is content-valid WebP, not only a renamed extension;
- alpha fixture remains alpha-capable;
- orientation fixture displays identically;
- ICC behavior is recorded;
- source untouched.

### Remove Metadata

- non-visual metadata fixtures no longer contain targeted metadata;
- orientation-6 fixture displays identically after orientation tag removal;
- ICC fixture retains color profile when representable;
- pixels are not resized except orientation width/height swap.

## Corpus growth rule

Every codec bug discovered during G3 or later production work should first become a minimal regression fixture/test before the implementation fix is accepted.
