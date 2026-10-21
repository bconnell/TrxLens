# Validation contract

The repository is designed for local validation. GitHub Actions is not required.

## Gates

Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1` on Windows with the .NET 10 SDK and PowerShell 5.1 or later. The script works only in the checked-out repository, checks available disk and Windows memory, runs one build worker, and stops on the first failed gate.

1. Verify required source, fixtures, attribution, and exact MCP tool names against documentation.
2. Restore declared NuGet dependencies serially.
3. Build the whole solution in Release with warnings as errors and one MSBuild worker.
4. Verify whitespace formatting without changing files.
5. Run xUnit parser, safety and comparison regression tests.
6. Run an actual stdio MCP client smoke process against the compiled server. This checks tool discovery, calls, failure evidence, comparison, malicious path rejection, the resource, and the prompt.

The script changes no global .NET settings. Its environment changes are process-local and restored. Build artifacts remain in ignored `bin` and `obj` directories. No files are deleted by the validation script.

## Release evidence

A merge-ready result requires an exit code of zero from the entire `verify.ps1` command for the **exact commit being merged**. A successful static inspection or an earlier branch test is not a substitute. Record the local run's terminal log, timestamp, SDK version and commit SHA before declaring full validation. Do not claim a screenshot, benchmark, build or test result without the corresponding observable artifact.

## Interpreting results

Counts derive from `UnitTestResult` elements. `Passed` and `Failed` are recognized case-insensitively. `Skipped` and `NotExecuted` are skipped. Other statuses are counted separately. A test present in the baseline but missing in the current run is **missing**, never resolved. A newly introduced failing test appears both among `addedTests` and `newFailures`.

## Validation status

Local .NET compilation and runtime gates have not been executed by the authoring environment. They are mandatory before merging or advertising a verified release.
