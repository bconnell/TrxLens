using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace TrxLens;

[McpServerToolType]
public sealed class TrxTools
{
    [McpServerTool(Name = "list_test_runs", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists available top-level TRX filenames and file metadata in the configured results directory.")]
    public static IReadOnlyList<RunFile> ListTestRuns(TrxRepository repository) =>
        Guard(() => repository.ListRuns());

    [McpServerTool(Name = "summarize_test_run", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Counts test outcomes from actual UnitTestResult entries, not potentially stale TRX counter attributes.")]
    public static TestSummary SummarizeTestRun(
        TrxRepository repository,
        [Description("Exact TRX filename returned by list_test_runs. Never use a full path.")] string run) =>
        Guard(() => repository.LoadRun(run).Summary);

    [McpServerTool(Name = "get_failed_tests", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns actual failed test names, identities, error messages and stack traces from a TRX run.")]
    public static IReadOnlyList<TestCase> GetFailedTests(
        TrxRepository repository,
        [Description("Exact TRX filename returned by list_test_runs.")] string run) =>
        Guard(() => repository.LoadRun(run).Tests.Where(
            test => string.Equals(test.Outcome, "Failed", StringComparison.OrdinalIgnoreCase)).ToArray());

    [McpServerTool(Name = "compare_test_runs", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Compares a baseline and current TRX run by test identity; separates new failures, persistent failures, resolved failures, missing tests and added tests.")]
    public static RunComparison CompareTestRuns(
        TrxRepository repository,
        [Description("Baseline TRX filename from list_test_runs.")] string baseline,
        [Description("Current TRX filename from list_test_runs.")] string current) =>
        Guard(() => repository.CompareRuns(baseline, current));

    private static T Guard<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidDataException or FileNotFoundException
            or DirectoryNotFoundException or UnauthorizedAccessException
            or IOException or System.Security.SecurityException)
        {
            throw new McpException(exception is UnauthorizedAccessException
                ? "Access to the configured results directory was denied."
                : exception is IOException && exception is not InvalidDataException && exception is not FileNotFoundException
                    ? "The TRX result could not be read."
                    : exception.Message);
        }
    }
}
