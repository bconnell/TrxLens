# PowerShell 5.1 compatible. Read-only repository text and configuration checks.
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$required = @(
    'README.md',
    'LICENSE',
    'TrxLens.slnx',
    'src/TrxLens/Program.cs',
    'src/TrxLens/TrxRepository.cs',
    'src/TrxLens/TrxTools.cs',
    'src/TrxLens/TrxExtras.cs',
    'tests/TrxLens.Tests/TrxRepositoryTests.cs',
    'tests/TrxLens.Smoke/Program.cs',
    'samples/baseline.trx',
    'samples/current.trx',
    'docs/SECURITY.md',
    'docs/VALIDATION.md'
)
foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $relative) -PathType Leaf)) {
        throw "Required repository file is absent: $relative"
    }
}
$readme = [IO.File]::ReadAllText((Join-Path $root 'README.md'))
$license = [IO.File]::ReadAllText((Join-Path $root 'LICENSE'))
$source = [IO.File]::ReadAllText((Join-Path $root 'src/TrxLens/TrxTools.cs'))
$program = [IO.File]::ReadAllText((Join-Path $root 'src/TrxLens/Program.cs'))
if ($license -notmatch 'Copyright \(c\) 2026 Brandon Connell') {
    throw 'Original copyright attribution is missing.'
}
foreach ($name in @('list_test_runs', 'summarize_test_run', 'get_failed_tests', 'compare_test_runs')) {
    if (-not $readme.Contains($name) -or -not $source.Contains($name)) {
        throw "MCP tool not described or not implemented: $name"
    }
}
if (-not $program.Contains('LogToStandardErrorThreshold') -or -not $program.Contains('TRXLENS_RESULTS_DIR')) {
    throw 'Stdio stdout separation or root configuration is missing.'
}
if ($readme -match '(?im)^\s*(all tests pass|fully verified|production.ready)\b') {
    throw 'Unsupported blanket completion language found.'
}
Write-Host 'Repository structure, text, attribution and basic configuration checks passed.'
