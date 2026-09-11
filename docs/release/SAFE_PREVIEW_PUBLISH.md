# Safe Preview Publish

The repository can produce a runnable Windows preview bundle without enabling global native activation or claiming a
production WebP codec.

## Publish

```powershell
./scripts/publish-preview.ps1 -OutputDirectory ./artifacts/publish/smartdrag-preview
```

For a self-contained bundle (larger output, no preinstalled .NET runtime required):

```powershell
./scripts/publish-preview.ps1 -SelfContained -OutputDirectory ./artifacts/publish/smartdrag-preview-self-contained
```

The script validates the repository and image corpus, restores the `win-x64` publish graph, publishes Release output,
runs the published executable's 21-check self-check, and writes `manifest.json` with SHA-256 hashes.

An already-created bundle can be verified independently without launching the executable:

```powershell
python ./scripts/verify_preview_publish.py ./artifacts/publish/smartdrag-preview
```

The verifier checks the manifest schema, exact file set, byte counts, SHA-256 hashes, and the embedded 21/21
self-check report. Publishing invokes the same verifier after writing the manifest.

## Safety invariants

- output must be new or empty and must remain below repository `artifacts/`;
- `ProductionActivationPolicy.NativeActivationEnabled` must still be explicitly `false`;
- the manifest records `webpEncoder: unavailable-until-G3`;
- no source fixture or user file is modified;
- the bundle is a safe preview artifact, not a production activation package.

## Verification

The Release framework-dependent bundle was published successfully on Windows 10 / .NET 8.0.424. It contained 23
hashed runtime files plus the manifest and self-check report; the published executable returned a passing 21/21
self-check. The self-contained variant remains an optional packaging mode and must be measured before shipping.
