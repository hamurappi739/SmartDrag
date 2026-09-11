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

function Resolve-PythonCommand {
    $candidates = @()
    if ($env:SMARTDRAG_PYTHON) {
        $candidates += [pscustomobject]@{
            Name = $env:SMARTDRAG_PYTHON
            Prefix = @()
            Explicit = $true
        }
    }

    $candidates += [pscustomobject]@{ Name = 'py'; Prefix = @('-3'); Explicit = $false }
    $candidates += [pscustomobject]@{ Name = 'python'; Prefix = @(); Explicit = $false }
    $candidates += [pscustomobject]@{ Name = 'python3'; Prefix = @(); Explicit = $false }

    foreach ($candidate in $candidates) {
        $command = Get-Command -Name $candidate.Name -ErrorAction SilentlyContinue
        if (-not $command) {
            if ($candidate.Explicit) {
                throw "SMARTDRAG_PYTHON points to '$($candidate.Name)', but that command or path was not found."
            }

            continue
        }

        $commandPath = if ($command.Path) { $command.Path } else { $command.Source }
        & $commandPath @($candidate.Prefix) '--version' *> $null
        if ($LASTEXITCODE -eq 0) {
            return [pscustomobject]@{
                FilePath = $commandPath
                Prefix = $candidate.Prefix
            }
        }

        if ($candidate.Explicit) {
            throw "SMARTDRAG_PYTHON '$($candidate.Name)' was found but could not execute '--version'."
        }
    }

    throw "Python 3 was not found. Install Python 3 or set SMARTDRAG_PYTHON to its executable path."
}

Push-Location (Join-Path $PSScriptRoot '..')
try {
    $python = Resolve-PythonCommand
    Invoke-Checked $python.FilePath ($python.Prefix + @('.\scripts\validate_repo.py', '--allow-build-artifacts'))
    Invoke-Checked $python.FilePath ($python.Prefix + @('.\scripts\validate_image_corpus.py'))
    Invoke-Checked $python.FilePath ($python.Prefix + @('.\scripts\audit_image_corpus.py', '--json-out', '.\artifacts\g3-corpus\audit-latest.json'))

    Invoke-Checked dotnet @('--info')
    Invoke-Checked dotnet @('restore', '.\SmartDrag.sln', '--configfile', '.\NuGet.Config')
    Invoke-Checked dotnet @('build', '.\SmartDrag.sln', '-c', 'Debug', '--no-restore')
    Invoke-Checked dotnet @('test', '.\SmartDrag.sln', '-c', 'Debug', '--no-build')
}
finally {
    Pop-Location
}
