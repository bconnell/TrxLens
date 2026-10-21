using System.Xml;
using System.Xml.Linq;

namespace TrxLens;

/// <summary>
/// Reads only top-level TRX files within a fixed, explicitly configured directory.
/// No arbitrary paths are accepted from MCP clients.
/// </summary>
public sealed class TrxRepository
{
    public const int MaximumRuns = 100;
    public const long MaximumFileBytes = 8 * 1024 * 1024;
    public const int MaximumTests = 10_000;
    public const int MaximumEvidenceCharacters = 4_096;

    private readonly string _directory;

    public TrxRepository(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
        DirectoryInfo info = new(_directory);
        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("The results directory must exist and must not be a symbolic link.");
        }
    }

    public IReadOnlyList<RunFile> ListRuns()
    {
        return FindFiles()
            .Select(file => new RunFile(file.Name, file.Length, file.LastWriteTimeUtc))
            .ToArray();
    }

    public TestRun LoadRun(string run)
    {
        string path = GetSafeFilePath(run);
        FileInfo info = new(path);
        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new FileNotFoundException("The requested TRX run does not exist or cannot be read safely.");
        }

        if (info.Length > MaximumFileBytes)
        {
            throw new InvalidDataException("The TRX run exceeds the 8 MiB file size limit.");
        }

        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumFileBytes,
            MaxCharactersFromEntities = 0,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true
        };

        XDocument document;
        try
        {
            using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using XmlReader reader = XmlReader.Create(file, settings);
            document = XDocument.Load(reader);
        }
        catch (XmlException)
        {
            throw new InvalidDataException("The TRX document is malformed or contains prohibited XML.");
        }

        XElement root = document.Root ?? throw new InvalidDataException("The TRX document is empty.");
        if (root.Name.LocalName != "TestRun")
        {
            throw new InvalidDataException("The document does not contain a TRX TestRun root.");
        }

        XNamespace ns = root.Name.Namespace;
        Dictionary<string, string> classes = root.Element(ns + "TestDefinitions")?
            .Elements(ns + "UnitTest")
            .Where(test => (string?)test.Attribute("id") is not null)
            .GroupBy(test => (string)test.Attribute("id")!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (string?)group.First().Element(ns + "TestMethod")?.Attribute("className") ?? "",
                StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);

        XElement? results = root.Element(ns + "Results");
        List<TestCase> tests = [];
        Dictionary<string, int> duplicates = new(StringComparer.Ordinal);
        foreach (XElement result in results?.Elements(ns + "UnitTestResult") ?? [])
        {
            if (tests.Count >= MaximumTests)
            {
                throw new InvalidDataException("The TRX run exceeds the 10,000 test result limit.");
            }

            string name = (string?)result.Attribute("testName") ?? "";
            string testId = (string?)result.Attribute("testId") ?? "";
            string className = classes.GetValueOrDefault(testId, "");
            string key = string.IsNullOrWhiteSpace(className) ? name : className + "." + name;
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidDataException("A test result has no identifiable name.");
            }

            // A repeated test identity can occur in data-driven runs. Preserve all executions.
            int occurrence = duplicates.GetValueOrDefault(key) + 1;
            duplicates[key] = occurrence;
            if (occurrence > 1)
            {
                key += "#" + occurrence.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            XElement? failure = result.Element(ns + "Output")?.Element(ns + "ErrorInfo");
            tests.Add(new TestCase(
                key,
                name,
                (string?)result.Attribute("outcome") ?? "Unknown",
                TrimEvidence((string?)failure?.Element(ns + "Message")),
                TrimEvidence((string?)failure?.Element(ns + "StackTrace"))));
        }

        return new TestRun(run, Summarize(tests), tests);
    }

    public RunComparison CompareRuns(string baseline, string current)
    {
        TestRun older = LoadRun(baseline);
        TestRun newer = LoadRun(current);
        Dictionary<string, TestCase> before = older.Tests.ToDictionary(test => test.Key, StringComparer.Ordinal);
        Dictionary<string, TestCase> after = newer.Tests.ToDictionary(test => test.Key, StringComparer.Ordinal);

        TestCase[] newFailures = newer.Tests
            .Where(test => IsFailed(test) && (!before.TryGetValue(test.Key, out TestCase? previous) || !IsFailed(previous)))
            .ToArray();
        TestCase[] persistent = newer.Tests
            .Where(test => IsFailed(test) && before.TryGetValue(test.Key, out TestCase? previous) && IsFailed(previous))
            .ToArray();
        TestCase[] resolved = older.Tests
            .Where(test => IsFailed(test) && after.TryGetValue(test.Key, out TestCase? updated) && !IsFailed(updated))
            .ToArray();

        return new RunComparison(
            baseline,
            current,
            older.Summary,
            newer.Summary,
            newFailures,
            persistent,
            resolved,
            before.Keys.Except(after.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            after.Keys.Except(before.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }

    private static bool IsFailed(TestCase test) =>
        string.Equals(test.Outcome, "Failed", StringComparison.OrdinalIgnoreCase);

    private static TestSummary Summarize(IReadOnlyList<TestCase> tests)
    {
        int passed = tests.Count(test => string.Equals(test.Outcome, "Passed", StringComparison.OrdinalIgnoreCase));
        int failed = tests.Count(IsFailed);
        int skipped = tests.Count(test => string.Equals(test.Outcome, "NotExecuted", StringComparison.OrdinalIgnoreCase)
            || string.Equals(test.Outcome, "Skipped", StringComparison.OrdinalIgnoreCase));
        return new TestSummary(tests.Count, passed, failed, skipped, tests.Count - passed - failed - skipped);
    }

    private static string TrimEvidence(string? value)
    {
        return value is null
            ? ""
            : value.Length <= MaximumEvidenceCharacters
                ? value
                : value[..MaximumEvidenceCharacters] + " [truncated]";
    }

    private string GetSafeFilePath(string run)
    {
        if (string.IsNullOrWhiteSpace(run)
            || run.Length > 240
            || run is "." or ".."
            || !string.Equals(Path.GetFileName(run), run, StringComparison.Ordinal)
            || run.Contains('/')
            || run.Contains('\\')
            || !string.Equals(Path.GetExtension(run), ".trx", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Provide a TRX filename from list_test_runs, not a path.", nameof(run));
        }

        // An untrusted run name must match a file actually enumerated from this directory.
        FileInfo? listed = FindFiles().FirstOrDefault(file => string.Equals(file.Name, run, StringComparison.Ordinal));
        if (listed is null)
        {
            throw new FileNotFoundException("The requested TRX filename was not found.");
        }

        return listed.FullName;
    }

    private IReadOnlyList<FileInfo> FindFiles()
    {
        FileInfo[] files = new DirectoryInfo(_directory)
            .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
            .Where(file => string.Equals(file.Extension, ".trx", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file.Name, StringComparer.Ordinal)
            .Take(MaximumRuns + 1)
            .ToArray();

        if (files.Length > MaximumRuns)
        {
            throw new InvalidDataException("The results directory contains more than 100 TRX runs.");
        }

        return files.Where(file => (file.Attributes & FileAttributes.ReparsePoint) == 0).ToArray();
    }
}
