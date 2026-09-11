[CmdletBinding()]
param(
    [ValidateSet('p0', 'g2')]
    [string]$Mode = 'p0',
    [switch]$Smoke,
    [switch]$SkipAnalysis
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
    $python3 = Get-Command python3 -ErrorAction SilentlyContinue
    if ($python3) { return [pscustomobject]@{ FilePath = $python3.Source; Prefix = @() } }
    throw 'Python 3 was not found. Install Python 3 or set SMARTDRAG_PYTHON.'
}

Push-Location (Join-Path $PSScriptRoot '..')
try {
    $probeArguments = @("--mode=$Mode")
    if ($Smoke) {
        $probeArguments += '--smoke'
    }

    dotnet run --project .\tools\SmartDrag.Windows.Probe\SmartDrag.Windows.Probe.csproj -c Debug -- $probeArguments
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    if ($SkipAnalysis) {
        exit 0
    }

    $modeName = if ($Mode -eq 'g2') { 'G2ExplorerSelection' } else { 'P0SignalOnly' }
    $logDirectory = Join-Path (Get-Location) 'tools\SmartDrag.Windows.Probe\bin\Debug\net8.0-windows\logs'
    $latestLog = Get-ChildItem -LiteralPath $logDirectory -Filter "smartdrag-probe-$modeName-*.jsonl" -File |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $latestLog) {
        throw "Probe completed but no $Mode JSONL log was found in $logDirectory."
    }

    Write-Output "Analyzing probe log: $($latestLog.FullName)"
    $python = Resolve-Python
    $analysisArguments = @($latestLog.FullName, '--json-out', "$($latestLog.FullName).summary.json")
    if (-not $Smoke) {
        $analysisArguments += '--require-interaction'
    }
    $analysisCommand = $python.Prefix + @('.\scripts\analyze_probe_log.py') + $analysisArguments
    & $python.FilePath @analysisCommand
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
