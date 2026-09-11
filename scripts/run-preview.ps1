[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$NoBuild,
    [switch]$SelfCheck,
    [string]$ReportPath = 'artifacts/preview-self-check/launcher-latest.json'
)

$ErrorActionPreference = 'Stop'

function Invoke-Checked {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,
        [string[]]$ArgumentList = @()
    )

    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "Command '$FilePath $($ArgumentList -join ' ')' failed with exit code $LASTEXITCODE."
    }
}

Push-Location (Join-Path $PSScriptRoot '..')
try {
    $repoRoot = (Get-Location).Path
    $toolingRoot = Join-Path $repoRoot '.tooling'
    $env:DOTNET_CLI_HOME = Join-Path $toolingRoot 'dotnet-cli'
    $env:NUGET_PACKAGES = Join-Path $toolingRoot 'nuget-packages'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_NOLOGO = '1'

    if (-not $NoBuild) {
        Invoke-Checked dotnet @('build', '.\SmartDrag.sln', '-c', $Configuration, '--no-restore', '-m:1', '--verbosity', 'minimal')
    }

    $arguments = @('run', '--project', '.\src\SmartDrag.App', '-c', $Configuration, '--no-restore', '--no-build', '--')
    if ($SelfCheck) {
        $arguments += "--self-check=$ReportPath"
    }
    else {
        $arguments += '--preview'
    }

    Invoke-Checked dotnet $arguments
}
finally {
    Pop-Location
}
