using System.Text;
using TrxLens;
using Xunit;

namespace TrxLens.Tests;

public sealed class TrxRepositoryTests
{
    [Fact]
    public void SampleRunsProduceExpectedSummary()
    {
        using TestDirectory directory = new();
        directory.CopySample("baseline.trx");
        directory.CopySample("current.trx");
        TrxRepository repository = new(directory.Path);

        Assert.Equal(new[] { "baseline.trx", "current.trx" }, repository.ListRuns().Select(run => run.Name));
        Assert.Equal(new TestSummary(4, 1, 2, 1, 0), repository.LoadRun("baseline.trx").Summary);
        Assert.Equal(new TestSummary(4, 1, 2, 1, 0), repository.LoadRun("current.trx").Summary);
    }

    [Fact]
    public void ActualFailuresIncludeBoundedEvidence()
    {
        using TestDirectory directory = new();
        directory.CopySample("baseline.trx");
        TestRun run = new TrxRepository(directory.Path).LoadRun("baseline.trx");

        Assert.Equal(2, run.Tests.Count(test => test.Outcome == "Failed"));
        TestCase failed = Assert.Single(run.Tests, test => test.Name == "Charges");
        Assert.Equal("Sample.CheckoutTests.Charges", failed.Key);
        Assert.Equal("Expected total 42, received 41", failed.Message);
        Assert.Contains("CheckoutTests.Charges", failed.StackTrace, StringComparison.Ordinal);
    }

    [Fact]
    public void ComparisonSeparatesChangesAndDoesNotClaimMissingTestsResolved()
    {
        using TestDirectory directory = new();
        directory.CopySample("baseline.trx");
        directory.CopySample("current.trx");
        RunComparison comparison = new TrxRepository(directory.Path)
            .CompareRuns("baseline.trx", "current.trx");

        Assert.Equal("Sample.CheckoutTests.Loads", Assert.Single(comparison.NewFailures).Key);
        Assert.Equal("Sample.CheckoutTests.Charges", Assert.Single(comparison.PersistentFailures).Key);
        Assert.Equal("Sample.CheckoutTests.Refunds", Assert.Single(comparison.ResolvedFailures).Key);
        Assert.Equal("Sample.CheckoutTests.Legacy", Assert.Single(comparison.MissingTests));
        Assert.Equal("Sample.CheckoutTests.Taxes", Assert.Single(comparison.AddedTests));
    }

    [Theory]
    [InlineData("../outside.trx")]
    [InlineData("..\\outside.trx")]
    [InlineData("/tmp/outside.trx")]
    [InlineData("not-a-trx.txt")]
    [InlineData("")]
    [InlineData("..")]
    public void RejectsUntrustedFilenames(string input)
    {
        using TestDirectory directory = new();
        Assert.Throws<ArgumentException>(() => new TrxRepository(directory.Path).LoadRun(input));
    }

    [Theory]
    [InlineData("run\nother.trx")]
    [InlineData("run\rother.trx")]
    public void RejectsControlCharactersInRunNames(string input)
    {
        using TestDirectory directory = new();
        Assert.Throws<ArgumentException>(() => TrxRepository.ValidateRunFilename(input));
    }

    [Fact]
    public void MissingNamedRunIsRejected()
    {
        using TestDirectory directory = new();
        Assert.Throws<FileNotFoundException>(() => new TrxRepository(directory.Path).LoadRun("missing.trx"));
    }

    [Fact]
    public void RejectsExternalEntitiesAndDoctype()
    {
        using TestDirectory directory = new();
        directory.Write("bad.trx", "<!DOCTYPE TestRun [ <!ENTITY x SYSTEM 'file:///etc/passwd'> ]>" +
            "<TestRun><Results><UnitTestResult testName='x' outcome='Failed' /></Results></TestRun>");

        Assert.Throws<InvalidDataException>(() => new TrxRepository(directory.Path).LoadRun("bad.trx"));
    }

    [Fact]
    public void RejectsMalformedXmlAndNonTrxRoot()
    {
        using TestDirectory directory = new();
        directory.Write("broken.trx", "<TestRun><Results>");
        directory.Write("wrong.trx", "<Other />");
        TrxRepository repository = new(directory.Path);

        Assert.Throws<InvalidDataException>(() => repository.LoadRun("broken.trx"));
        Assert.Throws<InvalidDataException>(() => repository.LoadRun("wrong.trx"));
    }

    [Fact]
    public void RejectsLargeFiles()
    {
        using TestDirectory directory = new();
        directory.Write("huge.trx", new string('a', (int)TrxRepository.MaximumFileBytes + 1));
        Assert.Throws<InvalidDataException>(() => new TrxRepository(directory.Path).LoadRun("huge.trx"));
    }

    [Fact]
    public void RejectsUnboundedRunCounts()
    {
        using TestDirectory directory = new();
        for (int index = 0; index <= TrxRepository.MaximumRuns; index++)
        {
            directory.Write($"run-{index:000}.trx", "<TestRun />");
        }

        Assert.Throws<InvalidDataException>(() => new TrxRepository(directory.Path).ListRuns());
    }

    [Fact]
    public void RejectsUnboundedTestCounts()
    {
        using TestDirectory directory = new();
        StringBuilder xml = new("<TestRun><Results>");
        for (int index = 0; index <= TrxRepository.MaximumTests; index++)
        {
            xml.Append("<UnitTestResult testName='T").Append(index)
                .Append("' outcome='Passed' />");
        }

        xml.Append("</Results></TestRun>");
        directory.Write("many.trx", xml.ToString());
        Assert.Throws<InvalidDataException>(() => new TrxRepository(directory.Path).LoadRun("many.trx"));
    }

    [Fact]
    public void TruncatesEvidenceButKeepsActualFailure()
    {
        using TestDirectory directory = new();
        string evidence = new('z', TrxRepository.MaximumEvidenceCharacters + 99);
        directory.Write("evidence.trx", "<TestRun><Results><UnitTestResult testName='T' outcome='Failed'>" +
            "<Output><ErrorInfo><Message>" + evidence +
            "</Message></ErrorInfo></Output></UnitTestResult></Results></TestRun>");

        TestCase test = Assert.Single(new TrxRepository(directory.Path).LoadRun("evidence.trx").Tests);
        Assert.StartsWith(new string('z', TrxRepository.MaximumEvidenceCharacters), test.Message, StringComparison.Ordinal);
        Assert.EndsWith(" [truncated]", test.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateNamesRemainDistinct()
    {
        using TestDirectory directory = new();
        directory.Write("duplicate.trx", "<TestRun><Results>" +
            "<UnitTestResult testName='Data' outcome='Passed' />" +
            "<UnitTestResult testName='Data' outcome='Failed' />" +
            "</Results></TestRun>");
        TestRun run = new TrxRepository(directory.Path).LoadRun("duplicate.trx");

        Assert.Equal(2, run.Summary.Total);
        Assert.Equal("Data", run.Tests[0].Key);
        Assert.Equal("Data#2", run.Tests[1].Key);
    }

    [Fact]
    public void UnknownOutcomesAreCountedAsOther()
    {
        using TestDirectory directory = new();
        directory.Write("other.trx", "<TestRun><Results>" +
            "<UnitTestResult testName='One' outcome='Inconclusive' />" +
            "</Results></TestRun>");

        Assert.Equal(new TestSummary(1, 0, 0, 0, 1), new TrxRepository(directory.Path).LoadRun("other.trx").Summary);
    }

    [Fact]
    public void MissingDirectoryCannotBeConfigured()
    {
        using TestDirectory directory = new();
        Assert.Throws<IOException>(() => new TrxRepository(System.IO.Path.Combine(directory.Path, "uncreated")));
    }

    private sealed class TestDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            AppContext.BaseDirectory, "scratch", Guid.NewGuid().ToString("N"));

        public TestDirectory() => Directory.CreateDirectory(Path);

        public void Write(string filename, string contents) =>
            File.WriteAllText(System.IO.Path.Combine(Path, filename), contents, Encoding.UTF8);

        public void CopySample(string filename) =>
            File.Copy(System.IO.Path.Combine(AppContext.BaseDirectory, "samples", filename),
                System.IO.Path.Combine(Path, filename));

        public void Dispose()
        {
            // Only this test's unique, application-owned scratch directory is removed.
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
