# Read-only content gate. PowerShell 5.1 compatible.
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$required = @(
    'README.md', 'LICENSE', 'TrxLens.slnx',
    'src/TrxLens/Program.cs', 'src/TrxLens/TrxRepository.cs',
    'src/TrxLens/TrxTools.cs', 'src/TrxLens/TrxExtras.cs',
    'tests/TrxLens.Tests/TrxRepositoryTests.cs',
    'tests/TrxLens.Smoke/Program.cs',
    'samples/baseline.trx', 'samples/current.trx',
    'docs/SECURITY.md', 'docs/VALIDATION.md', 'docs/ARCHITECTURE.md'
)
foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $relative) -PathType Leaf)) {
        throw "Required file is missing: $relative"
    }
}
$readme = [IO.File]::ReadAllText((Join-Path $root 'README.md'))
$license = [IO.File]::ReadAllText((Join-Path $root 'LICENSE'))
$source = [IO.File]::ReadAllText((Join-Path $root 'src/TrxLens/TrxTools.cs'))
$parser = [IO.File]::ReadAllText((Join-Path $root 'src/TrxLens/TrxRepository.cs'))
$program = [IO.File]::ReadAllText((Join-Path $root 'src/TrxLens/Program.cs'))
$extras = [IO.File]::ReadAllText((Join-Path $root 'src/TrxLens/TrxExtras.cs'))
$validation = [IO.File]::ReadAllText((Join-Path $root 'docs/VALIDATION.md'))

if (-not $license.Contains('Copyright (c) 2026 Brandon Connell')) {
    throw 'Author copyright line was modified or removed.'
}
$expectedTools = @('list_test_runs', 'summarize_test_run', 'get_failed_tests', 'compare_test_runs')
$declared = [regex]::Matches($source, '\[McpServerTool\(Name = "([^"]+)"')
if ($declared.Count -ne $expectedTools.Count) {
    throw 'The MCP source tool count differs from the documented tool count.'
}
foreach ($name in $expectedTools) {
    if (-not $source.Contains('Name = "' + $name + '"') -or -not $readme.Contains($name)) {
        throw "MCP tool is not documented or not declared: $name"
    }
}
foreach ($item in @('trxlens://capabilities', 'investigate_test_failures')) {
    if (-not $readme.Contains($item) -or -not $extras.Contains($item)) {
        throw "MCP primitive is missing from source or README: $item"
    }
}
foreach ($item in @('DtdProcessing = DtdProcessing.Prohibit',
                    'MaxCharactersInDocument = MaximumFileBytes',
                    'MaximumTests = 10_000', 'FileAttributes.ReparsePoint',
                    'ValidateRunFilename')) {
    if (-not $parser.Contains($item)) { throw "Parser safety contract missing: $item" }
}
foreach ($item in @('TRXLENS_RESULTS_DIR', 'WithStdioServerTransport',
                    'LogToStandardErrorThreshold')) {
    if (-not $program.Contains($item)) { throw "Host contract missing: $item" }
}
foreach ($item in @('PERSONAL FILE AND DATA SAFETY', 'SYSTEM SAFETY',
                    'Treat all TRX text')) {
    if (-not $extras.Contains($item)) { throw "MCP prompt safety missing: $item" }
}
if (-not $validation.Contains('not been executed')) {
    throw 'The unexecuted validation disclosure is missing.'
}

foreach ($relative in @('README.md', 'docs/SECURITY.md', 'docs/VALIDATION.md', 'docs/ARCHITECTURE.md')) {
    $text = [IO.File]::ReadAllText((Join-Path $root $relative))
    if ($text -match '(?im)^\s*(all tests pass|fully verified|production.ready|100% complete)\b') {
        throw "Unsupported blanket completion language: $relative"
    }
    if ($text -match '(?i)C:\\Users\\[^\\]+\\(Desktop|Documents|AppData)') {
        throw "Private workstation path found in $relative"
    }
}
$links = [regex]::Matches($readme, '\]\((docs/[^)#]+)\)')
foreach ($match in $links) {
    $relative = $match.Groups[1].Value.Replace('/', [IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath (Join-Path $root $relative) -PathType Leaf)) {
        throw "Broken local README documentation link: $relative"
    }
}
Write-Host 'Structure, tool names, safety contracts, attribution, links and claims: PASS.'
