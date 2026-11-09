# Architecture

The entire runtime is a single stdio-hosted .NET console application.

- `Program.cs` validates the fixed results directory, builds the DI host, registers MCP primitives, and directs logs to stderr.
- `TrxRepository.cs` owns path resolution, bounded enumeration, XML parsing, result normalization and comparisons.
- `Models.cs` defines immutable response records.
- `TrxTools.cs` exposes exactly four read-only MCP tools and maps expected input/file errors to MCP tool errors.
- `TrxExtras.cs` exposes one static capabilities resource and one reusable investigation prompt.
- `TrxLens.Tests` tests parser and comparison behavior with isolated scratch data.
- `TrxLens.Smoke` launches the built server and uses the official MCP SDK as a real protocol client.

Data flow: MCP client → SDK stdio transport → tool input validation → bounded TRX repository → immutable records → MCP response. The resource and prompt contain static guidance, no filesystem reads. There is no persistence layer, background worker, network interface or scheduler.

Comparison keys use the TRX test's `TestMethod.className` and `UnitTestResult.testName`; absent class names fall back to the result name. Repeated keys receive a deterministic occurrence suffix. Missing tests remain distinct from tests whose failures were genuinely resolved.
