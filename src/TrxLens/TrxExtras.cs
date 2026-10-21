using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace TrxLens;

[McpServerResourceType]
public static class TrxResources
{
    [McpServerResource(UriTemplate = "trxlens://capabilities", Name = "TrxLens capabilities", MimeType = "application/json")]
    [Description("Documents the server's read-only scope, limits, tool names, and interpretation rules.")]
    public static string GetCapabilities() => JsonSerializer.Serialize(new
    {
        product = "TrxLens",
        transport = "stdio",
        access = "Read-only top-level TRX files in TRXLENS_RESULTS_DIR",
        tools = new[] { "list_test_runs", "summarize_test_run", "get_failed_tests", "compare_test_runs" },
        maximumRuns = TrxRepository.MaximumRuns,
        maximumFileBytes = TrxRepository.MaximumFileBytes,
        maximumTestsPerRun = TrxRepository.MaximumTests,
        evidenceCharacterLimit = TrxRepository.MaximumEvidenceCharacters,
        note = "Missing tests are not classified as resolved. Comparison identities use TRX className and testName."
    });
}

[McpServerPromptType]
public static class TrxPrompts
{
    [McpServerPrompt(Name = "investigate_test_failures")]
    [Description("Provides a reproducible read-only workflow for explaining differences between two TRX runs.")]
    public static ChatMessage InvestigateTestFailures(
        [Description("Baseline TRX filename.")] string baseline,
        [Description("Current TRX filename.")] string current) =>
        new(ChatRole.User,
            "Use list_test_runs to confirm the files exist, then compare_test_runs with baseline '" +
            baseline + "' and current '" + current +
            "'. Inspect failures with get_failed_tests. Quote only observed test messages or stack traces. " +
            "Distinguish new, persistent, resolved, added and missing tests. Never infer a root cause " +
            "when the TRX evidence is insufficient. Do not run code or modify files.");
}
