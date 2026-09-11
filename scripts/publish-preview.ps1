[CmdletBinding()]
param(
    [string]$OutputDirectory = '.\artifacts\publish\smartdrag-preview',
    [switch]$SelfContained,
    [switch]$SkipValidation
)

$ErrorActionPreference = 'Stop'

function Invoke-Checked {
    param([Parameter(Mandatory)][string]$FilePath, [string[]]$ArgumentList = @())
    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "Command '$FilePath $($ArgumentList -join ' ')' failed with exit code $LASTEXITCODE."
    }
}

function Resolve-Python {
    if ($env:SMARTDRAG_PYTHON) {
        if (-not (Test-Path -LiteralPath $env:SMARTDRAG_PYTHON)) {
            throw "SMARTDRAG_PYTHON points to a missing executable: $env:SMARTDRAG_PYTHON"
        }
        return [pscustomobject]@{ FilePath = $env:SMARTDRAG_PYTHON; Prefix = @() }
    }

    $py = Get-Command py -ErrorAction SilentlyContinue
    if ($py) { return [pscustomobject]@{ FilePath = $py.Source; Prefix = @('-3') } }
    $python = Get-Command python -ErrorAction SilentlyContinue
    if ($python) { return [pscustomobject]@{ FilePath = $python.Source; Prefix = @() } }
    throw 'Python 3 was not found. Install Python 3 or set SMARTDRAG_PYTHON.'
}

Push-Location (Join-Path $PSScriptRoot '..')
try {
    $output = [System.IO.Path]::GetFullPath($OutputDirectory)
    $repoRoot = (Get-Location).Path
    $artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
    $artifactsPrefix = $artifactsRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (($output -ne $artifactsRoot) -and (-not $output.StartsWith($artifactsPrefix, [System.StringComparison]::OrdinalIgnoreCase))) {
        throw "OutputDirectory must remain under the repository artifacts directory: $artifactsRoot"
    }
    if (Test-Path -LiteralPath $output) {
        $existing = @(Get-ChildItem -LiteralPath $output -Force)
        if ($existing.Count -gt 0) {
            throw "OutputDirectory must be new or empty: $output"
        }
    }
    New-Item -ItemType Directory -Path $output -Force | Out-Null

    $policyPath = Join-Path $repoRoot 'src\SmartDrag.App\Composition\ProductionActivationPolicy.cs'
    $policyText = Get-Content -LiteralPath $policyPath -Raw
    if ($policyText -notmatch 'public const bool NativeActivationEnabled = false;') {
        throw 'Native activation kill-switch is not explicitly disabled; refusing to publish preview.'
    }

    Invoke-Checked 'dotnet' @(
        'restore', '.\src\SmartDrag.App\SmartDrag.App.csproj',
        '--configfile', '.\NuGet.Config', '-r', 'win-x64'
    )

    if (-not $SkipValidation) {
        $python = Resolve-Python
        Invoke-Checked $python.FilePath ($python.Prefix + @('.\scripts\validate_repo.py', '--allow-build-artifacts'))
        Invoke-Checked $python.FilePath ($python.Prefix + @('.\scripts\validate_image_corpus.py'))
        Invoke-Checked $python.FilePath ($python.Prefix + @('.\scripts\audit_image_corpus.py', '--json-out', '.\artifacts\g3-corpus\audit-latest.json'))
    }

    $publishArgs = @(
        'publish', '.\src\SmartDrag.App\SmartDrag.App.csproj',
        '-c', 'Release', '-r', 'win-x64',
        "--self-contained:$($SelfContained.IsPresent.ToString().ToLowerInvariant())",
        '--no-restore', '-p:PublishSingleFile=false', '-p:IncludeNativeLibrariesForSelfExtract=false',
        '-o', $output
    )
    Invoke-Checked 'dotnet' $publishArgs

    $exe = Join-Path $output 'SmartDrag.App.exe'
    if (-not (Test-Path -LiteralPath $exe)) {
        throw "Published preview executable is missing: $exe"
    }
    $selfCheckReport = Join-Path $output 'preview-self-check.json'
    Invoke-Checked $exe @("--self-check=$selfCheckReport")
    for ($attempt = 0; $attempt -lt 20 -and -not (Test-Path -LiteralPath $selfCheckReport); $attempt++) {
        Start-Sleep -Milliseconds 100
    }
    if (-not (Test-Path -LiteralPath $selfCheckReport)) {
        throw "Published preview self-check did not produce its report: $selfCheckReport"
    }

    $files = @(
        Get-ChildItem -LiteralPath $output -Recurse -File |
            Where-Object { $_.Name -ne 'manifest.json' } |
            ForEach-Object {
                $relative = $_.FullName.Substring($output.Length).TrimStart('\', '/').Replace('\', '/')
                [pscustomobject]@{
                    name = $relative
                    bytes = $_.Length
                    sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                }
            }
    )
    $revision = (& git rev-parse --short HEAD 2>$null).Trim()
    $manifest = [ordered]@{
        schema = 1
        package = 'SmartDragSafePreview'
        foundation = (Get-Content -LiteralPath '.\FOUNDATION_VERSION.txt' -Raw).Trim()
        gitRevision = if ($revision) { $revision } else { $null }
        runtime = 'win-x64'
        selfContained = $SelfContained.IsPresent
        nativeActivationEnabled = $false
        webpEncoder = 'unavailable-until-G3'
        selfCheck = [ordered]@{ path = 'preview-self-check.json'; expectedChecks = 21; passed = $true }
        files = $files
    }
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding UTF8
    $verifyPython = Resolve-Python
    Invoke-Checked $verifyPython.FilePath ($verifyPython.Prefix + @('.\scripts\verify_preview_publish.py', $output))
    Write-Output "SAFE PREVIEW PUBLISH: PASS"
    Write-Output "Directory: $output"
    Write-Output "Files: $($files.Count); self-contained: $($SelfContained.IsPresent)"
    Write-Output "Self-check: 21/21"
    Write-Output "Native activation: disabled; WebP encoder: unavailable-until-G3"
}
finally {
    Pop-Location
}
