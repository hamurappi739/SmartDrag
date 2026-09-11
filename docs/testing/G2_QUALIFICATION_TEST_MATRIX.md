# G2 Qualification Test Matrix

Run with:

```powershell
./scripts/run-probe.ps1 -Mode g2
```

Run without `-Smoke` for gate evidence. The wrapper analyzes the latest G2 JSONL and fails closed when no real
drag/drop interaction was recorded; it also writes a sibling `.summary.json` for retention with the raw log.

A row passes only when the visible behavior and the JSONL evidence both match expectations.

| Case | Expected overlay | Expected Drop acceptance | Required evidence |
|---|---:|---:|---|
| Explorer, one PNG | Yes | COPY on valid action | preflight Eligible; authoritative Supported; match=true |
| Explorer, one JPG/JPEG | Yes | COPY on valid action | same as PNG |
| Explorer, TXT | No | N/A | preflight Rejected/UnsupportedExtension |
| Explorer, PDF | No | N/A | preflight Rejected/UnsupportedExtension |
| Explorer, folder | No | N/A | DirectoryNotSupported |
| Explorer, two images selected | No | N/A | MultipleFiles |
| Explorer, missing/deleted selected file | No | NONE if reached | SourceMissing or mismatch |
| Two Explorer windows | Only source window payload | COPY only on match | root HWND maps to correct window |
| Windows 11 tabs | Only active/source tab payload | COPY only on match | no cross-tab selection leak |
| Desktop image | Yes if strategy supports Desktop | COPY on match | otherwise explicit G2 failure requiring fallback research |
| Hidden file extensions | Yes for PNG/JPEG | COPY on match | eligibility must use real path, not display text |
| Virtual/non-filesystem item | No | NONE | PathUnavailable/SourceMissing |
| Preflight path A, Drop path B | Overlay may already exist | NONE | preflightMatched=false |
| Ignore overlay; native drop elsewhere | No interference | native target decides | SmartDrag never commits action |
| Enter overlay then leave | Overlay remains transient | no commit | DragLeave logged, native drag continues |
| 100% -> 150% monitor | Yes when eligible | COPY on match | no focus theft; usable placement |

## Acceptance rule

G2 cannot be marked PASS until the supported Explorer surfaces are explicitly listed. If Desktop or Windows 11 tab behavior is unreliable, either:

- find a documented conservative fallback, or
- explicitly exclude that surface from the first shipping MVP.

Never silently fall back to showing overlay from drag-start signal alone.
