# TrxLens

A small, read-only MCP server for inspecting and comparing .NET test results in TRX format.

TrxLens runs locally over standard input/output and uses the official Model Context Protocol C# SDK. It reads a single explicitly configured directory of TRX reports. It never executes tests or changes a repository. The source is intentionally compact enough to inspect without a cloud service, web dashboard or database.

## What it exposes

| MCP tool | Purpose |
| --- | --- |
| `list_test_runs` | List top-level TRX filenames and metadata. |
| `summarize_test_run` | Count passed, failed, skipped and other test outcomes from real results. |
| `get_failed_tests` | Read failing test identities, messages and stack traces. |
| `compare_test_runs` | Identify new failures, persistent failures, resolved failures, missing tests and added tests. |

The server also exposes the `trxlens://capabilities` MCP resource and the `investigate_test_failures` MCP prompt.

## Requirements

- .NET 10 SDK to build and run from source.
- A local MCP client supporting stdio transport.
- One directory of `.trx` files. This repository includes two small example reports under `samples/`.

Microsoft's VSTest-based test runner can generate TRX files with `dotnet test --logger "trx;LogFileName=results.trx"`. The logger's output is usually under the test project's `TestResults` directory; configure that directory explicitly.

## Build and launch

From the repository root:

```powershell
dotnet restore TrxLens.slnx --disable-parallel
dotnet build TrxLens.slnx -c Release --no-restore -m:1
$env:TRXLENS_RESULTS_DIR = (Resolve-Path .\samples).Path
dotnet run --project .\src\TrxLens\TrxLens.csproj -c Release --no-build
```

The last command starts an MCP server waiting for a client. It produces MCP messages on stdout and diagnostics on stderr. For ordinary use, launch it **from your MCP client**, not as an interactive console session.

### MCP client configuration

Configure a stdio server with command `dotnet`, argument pointing to the absolute path of `src/TrxLens/bin/Release/net10.0/TrxLens.dll`, and environment variable `TRXLENS_RESULTS_DIR` pointing to the absolute path of your reports directory. Both paths must be local to the computer running the MCP client.

For example, in clients that accept this JSON configuration form, replace both placeholders with real absolute paths:

```json
{
  "mcpServers": {
    "trxlens": {
      "command": "dotnet",
      "args": ["C:\\path\\to\\TrxLens.dll"],
      "env": {
        "TRXLENS_RESULTS_DIR": "C:\\path\\to\\TestResults"
      }
    }
  }
}
```

Not every MCP client uses the same configuration schema. Refer to that client's documentation.

## Example: comparing two reports

The included `samples/baseline.trx` and `samples/current.trx` represent four tests each. Read them using `compare_test_runs` with `baseline="baseline.trx"` and `current="current.trx"`.

From the fixture XML, the expected distinctions are:

- New failure: `Sample.CheckoutTests.Loads`.
- Persistent failure: `Sample.CheckoutTests.Charges`.
- Resolved failure: `Sample.CheckoutTests.Refunds`.
- Missing test: `Sample.CheckoutTests.Legacy`.
- Added test: `Sample.CheckoutTests.Taxes`.

These are fixture expectations, **not** claims that a protocol run was executed.

## Local validation

The local gate is `scripts/verify.ps1`. Run it using PowerShell 5.1 or later:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1
```

It validates repository text and attribution, restores dependencies, builds with warnings as errors, verifies formatting, executes xUnit tests, and starts a real MCP stdio client for end-to-end checks. It stops immediately when a gate fails. The checks do not depend on GitHub Actions.

The current authoring environment did not have a .NET SDK, so no successful build or test result is asserted here. See [validation contract](docs/VALIDATION.md) for the exact evidence required before claiming a verified release.

## Security and limitations

Only top-level files in `TRXLENS_RESULTS_DIR` are accepted. The parser blocks DTD/XML entity expansion, rejects path traversal, ignores symlinked TRX files and enforces file, run and test limits. TRX messages and stack traces are untrusted data and may contain private information. Do not expose the reports directory unnecessarily or forward sensitive errors to remote AI models.

See [security boundaries](docs/SECURITY.md) and [architecture](docs/ARCHITECTURE.md) for implementation details and known comparison limitations.

## References

- [Official MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [MCP C# SDK getting started](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/getting-started.md)
- [Microsoft dotnet test and TRX logger](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-vstest)

## License

MIT. Copyright (c) 2026 Brandon Connell. See [LICENSE](LICENSE).
