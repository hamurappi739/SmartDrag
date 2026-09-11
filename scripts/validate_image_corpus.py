#!/usr/bin/env python3
from __future__ import annotations
import hashlib, json, sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
base=ROOT/'tests/fixtures/images'
manifest=json.loads((base/'CORPUS_MANIFEST.json').read_text(encoding='utf-8'))
errors=[]
for item in manifest['files']:
    path=base/item['name']
    if not path.exists():
        errors.append(f"missing: {item['name']}")
        continue
    data=path.read_bytes()
    if len(data)!=item['size']:
        errors.append(f"size mismatch: {item['name']}")
    actual=hashlib.sha256(data).hexdigest()
    if actual!=item['sha256']:
        errors.append(f"sha256 mismatch: {item['name']}")
required={'rgb-photo.jpg','rgb-basic.png','exif-orientation-6.jpg','png-text-metadata.png','alpha-gradient.png','icc-srgb.jpg','animated-negative.gif','not-an-image.png','truncated.jpg'}
found={x['name'] for x in manifest['files']}
for name in sorted(required-found): errors.append(f"required fixture absent from manifest: {name}")
if errors:
    print('IMAGE CORPUS VALIDATION FAILED')
    for e in errors: print('-',e)
    sys.exit(1)
print('IMAGE CORPUS VALIDATION PASSED')
print(f"Fixtures: {len(manifest['files'])}")
