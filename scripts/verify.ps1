# Run from PowerShell 5.1 or later. No GitHub Actions, global configuration changes or destructive cleanup.
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Invoke-Dotnet {
    param([Parameter(Mandatory=$true)][string[]]$Arguments)
    Write-Host ('dotnet ' + ($Arguments -join ' '))
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Validation failed (dotnet exit $LASTEXITCODE). Further gates were not run."
    }
}

$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnetCommand) { throw 'The .NET 10 SDK is not installed or is not on PATH.' }

$driveRoot = [IO.Path]::GetPathRoot($root)
$drive = New-Object System.IO.DriveInfo($driveRoot)
if ($drive.AvailableFreeSpace -lt 1073741824) {
    throw 'Less than 1 GiB disk space is free on the repository drive. Aborting.'
}
if ($env:OS -eq 'Windows_NT') {
    $os = Get-CimInstance Win32_OperatingSystem -ErrorAction Stop
    if (($os.FreePhysicalMemory * 1024) -lt 536870912) {
        throw 'Less than 512 MiB physical memory is free. Aborting before invoking the build.'
    }
}

$oldTelemetry = $env:DOTNET_CLI_TELEMETRY_OPTOUT
$oldFirstTime = $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE
try {
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    Push-Location $root
    try {
        & (Join-Path $PSScriptRoot 'check-content.ps1')
        if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw 'Content gate failed.' }
        Invoke-Dotnet -Arguments @('restore', 'TrxLens.slnx', '--disable-parallel')
        Invoke-Dotnet -Arguments @('build', 'TrxLens.slnx', '-c', 'Release', '--no-restore', '-m:1', '-warnaserror')
        Invoke-Dotnet -Arguments @('format', 'whitespace', 'TrxLens.slnx', '--verify-no-changes', '--no-restore')
        Invoke-Dotnet -Arguments @('test', 'tests/TrxLens.Tests/TrxLens.Tests.csproj',
            '-c', 'Release', '--no-build', '--no-restore', '--logger', 'console;verbosity=normal')
        Invoke-Dotnet -Arguments @('run', '--project', 'tests/TrxLens.Smoke/TrxLens.Smoke.csproj',
            '-c', 'Release', '--no-build', '--no-restore', '--',
            (Join-Path $root 'src/TrxLens/bin/Release/net10.0/TrxLens.dll'),
            (Join-Path $root 'samples'))
        Write-Host 'All local validation gates passed.'
    }
    finally { Pop-Location }
}
finally {
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = $oldTelemetry
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = $oldFirstTime
}
