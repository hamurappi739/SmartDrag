[CmdletBinding()]
param(
    [ValidateSet('p0', 'g2')]
    [string]$Mode = 'g2',
    [ValidateSet('explorer-folder', 'explorer-tab', 'desktop', 'unknown')]
    [string]$Surface = 'unknown',
    [string]$Dpi = 'unknown',
    [string]$Notes = '',
    [string]$OutputDirectory = '',
    [switch]$Smoke
)

$ErrorActionPreference = 'Stop'

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

function Invoke-Checked {
    param([Parameter(Mandatory)][string]$FilePath, [string[]]$ArgumentList = @())
    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "Command '$FilePath $($ArgumentList -join ' ')' failed with exit code $LASTEXITCODE."
    }
}

Push-Location (Join-Path $PSScriptRoot '..')
try {
    $repoRoot = (Get-Location).Path
    $artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts')).TrimEnd('\', '/')
    if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
        $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        $OutputDirectory = ".\artifacts\probe-evidence\$Mode-$stamp"
    }
    $output = [System.IO.Path]::GetFullPath($OutputDirectory)
    $artifactsPrefix = $artifactsRoot + [System.IO.Path]::DirectorySeparatorChar
    if (($output -eq $artifactsRoot) -or (-not $output.StartsWith($artifactsPrefix, [System.StringComparison]::OrdinalIgnoreCase))) {
        throw "OutputDirectory must remain below the repository artifacts directory: $artifactsRoot"
    }
    if (Test-Path -LiteralPath $output) {
        $existing = @(Get-ChildItem -LiteralPath $output -Force)
        if ($existing.Count -gt 0) {
            throw "OutputDirectory must be new or empty: $output"
        }
    }
    if ($Notes -match '[\\/]' -or $Notes -match '(?i)https?://') {
        throw 'Notes must be a short operator note without paths or URLs.'
    }
    New-Item -ItemType Directory -Path $output -Force | Out-Null

    $logDirectory = Join-Path $repoRoot 'tools\SmartDrag.Windows.Probe\bin\Debug\net8.0-windows\logs'
    $modeName = if ($Mode -eq 'g2') { 'G2ExplorerSelection' } else { 'P0SignalOnly' }
    $before = @()
    if (Test-Path -LiteralPath $logDirectory) {
        $before = @(Get-ChildItem -LiteralPath $logDirectory -Filter "smartdrag-probe-$modeName-*.jsonl" -File |
            Select-Object -ExpandProperty FullName)
    }

    & (Join-Path $repoRoot 'scripts\run-probe.ps1') -Mode $Mode -Smoke:$Smoke.IsPresent
    if ($LASTEXITCODE -ne 0) {
        throw "Probe run failed. No evidence package was created: $Mode"
    }

    $latest = Get-ChildItem -LiteralPath $logDirectory -Filter "smartdrag-probe-$modeName-*.jsonl" -File |
        Where-Object { $before -notcontains $_.FullName } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $latest) {
        $latest = Get-ChildItem -LiteralPath $logDirectory -Filter "smartdrag-probe-$modeName-*.jsonl" -File |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
    }
    if ($null -eq $latest) { throw "Probe completed but no $Mode JSONL log was found." }

    $summary = "$($latest.FullName).summary.json"
    if (-not (Test-Path -LiteralPath $summary)) {
        throw "Analyzer summary is missing beside the probe log: $summary"
    }

    $python = Resolve-Python
    $packageArgs = $python.Prefix + @(
        '.\scripts\package_probe_evidence.py', $latest.FullName,
        '--summary', $summary,
        '--out-dir', $output,
        '--surface', $Surface,
        '--dpi', $Dpi,
        '--notes', $Notes)
    Invoke-Checked $python.FilePath $packageArgs
    Write-Output "PROBE EVIDENCE CAPTURE: PASS"
    Write-Output "Mode: $Mode; smoke: $($Smoke.IsPresent)"
    Write-Output "Package: $output"
    Write-Output "Raw log: $($latest.FullName)"
    Write-Output "Manual matrix required: yes"
}
finally {
    Pop-Location
}
