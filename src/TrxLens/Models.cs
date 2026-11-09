namespace TrxLens;

public sealed record RunFile(string Name, long Bytes, DateTime LastModifiedUtc);

public sealed record TestCase(
    string Key,
    string Name,
    string Outcome,
    string Message,
    string StackTrace);

public sealed record TestSummary(int Total, int Passed, int Failed, int Skipped, int Other);

public sealed record TestRun(string Name, TestSummary Summary, IReadOnlyList<TestCase> Tests);

public sealed record RunComparison(
    string Baseline,
    string Current,
    TestSummary BaselineSummary,
    TestSummary CurrentSummary,
    IReadOnlyList<TestCase> NewFailures,
    IReadOnlyList<TestCase> PersistentFailures,
    IReadOnlyList<TestCase> ResolvedFailures,
    IReadOnlyList<string> MissingTests,
    IReadOnlyList<string> AddedTests);
