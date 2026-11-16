namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// Production code reads the current time only through <c>IClock</c> (backed by the registered <see cref="TimeProvider"/>),
/// so a test that replaces the provider moves every reading and a time window never depends on the moment the test runs
/// (issue 1208). No source under <c>backend/src</c> reads <c>DateTimeOffset.UtcNow</c>, <c>DateTime.UtcNow</c>,
/// <c>DateTime.Now</c>, <c>DateTimeOffset.Now</c> or <c>DateTime.Today</c>. <c>SystemClock</c> itself asks the
/// <see cref="TimeProvider"/>, so no file is exempt. The <c>Hostpanel.Sdk</c> submodule is a separate repository
/// with its own clock and is not scanned.
/// </summary>
public sealed class NoDirectClockTests
{
    /// <summary>The solution directory.</summary>
    private static string BackendRoot => UseCaseShapeTests.FindBackendRoot(AppContext.BaseDirectory);

    /// <summary>Every authored C# source under <c>backend/src</c> outside the SDK submodule, as a path relative to it and its text.</summary>
    /// <returns>The sources; build output is not authored code.</returns>
    private static List<(string Name, string Text)> Sources()
    {
        var src = Path.Combine(BackendRoot, "src");
        return Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Select(path => (Name: Path.GetRelativePath(src, path).Replace('\\', '/'), Path: path))
            .Where(file => !file.Name.StartsWith("Hostpanel.Sdk/", StringComparison.Ordinal)
                && !file.Name.Contains("/obj/", StringComparison.Ordinal)
                && !file.Name.Contains("/bin/", StringComparison.Ordinal))
            .Select(file => (file.Name, File.ReadAllText(file.Path)))
            .ToList();
    }

    /// <summary>The scan reaches the host's source files, so an empty result means the rule held and not that nothing was read.</summary>
    [Fact]
    public void Sources_AreRead()
    {
        Sources().Should().Contain(source => source.Name == "Innovayse.Hostpanel.Infrastructure/Time/SystemClock.cs");
    }

    /// <summary>No source under <c>backend/src</c> reads the system clock directly.</summary>
    [Fact]
    public void Sources_ReadTimeOnlyThroughTheClock()
    {
        DirectClockScan.Offenders(Sources())
            .Should().BeEmpty("time is read through IClock, which reads the registered TimeProvider");
    }

    /// <summary>The scan reports each planted direct read, so the rule is proven to bite.</summary>
    [Fact]
    public void Rule_FailsOnPlantedDirectReads()
    {
        const string planted = """
            var a = DateTimeOffset.UtcNow;
            var b = System.DateTime.UtcNow;
            var c = DateTime.Now;
            var d = DateTimeOffset . Now;
            var e = DateTime.Today;
            """;

        DirectClockScan.Offenders([("Planted.cs", planted)]).Should().BeEquivalentTo(
            "Planted.cs: DateTimeOffset.UtcNow",
            "Planted.cs: DateTime.UtcNow",
            "Planted.cs: DateTime.Now",
            "Planted.cs: DateTimeOffset.Now",
            "Planted.cs: DateTime.Today");
    }

    /// <summary>A comment that only names a clock read, and a type that merely ends in the name, are not reads.</summary>
    [Fact]
    public void Rule_IgnoresCommentsAndOtherNames()
    {
        const string benign = """
            /// <summary>Not DateTimeOffset.UtcNow.</summary>
            // DateTime.Now is forbidden
            /* DateTime.Today */
            var a = time.GetUtcNow() + MyDateTime.Now;
            """;

        DirectClockScan.Offenders([("Benign.cs", benign)]).Should().BeEmpty();
    }

    /// <summary>
    /// Text that only looks like a comment inside a string literal does not hide a read on the same line: the scan reads the
    /// syntax, not the characters, so a <c>//</c> or <c>/*</c> inside a string is just text.
    /// </summary>
    [Fact]
    public void Rule_FindsReadsBesideCommentLookalikesInStrings()
    {
        const string planted = """
            var a = "//"; var b = DateTime.UtcNow;
            var c = "/*"; var d = DateTimeOffset.Now; var e = "*/";
            var f = @"http://example.test/*"; var g = DateTime.Today;
            """;

        DirectClockScan.Offenders([("Planted.cs", planted)]).Should().BeEquivalentTo(
            "Planted.cs: DateTime.UtcNow",
            "Planted.cs: DateTimeOffset.Now",
            "Planted.cs: DateTime.Today");
    }

    /// <summary>
    /// The scan checks the <c>Type.Member</c> form only: a bare member brought in by <c>using static</c> and a read through an alias
    /// of the type are out of scope, while the same source's written-out read is still found.
    /// </summary>
    [Fact]
    public void Rule_ChecksTheTypeMemberFormOnly()
    {
        const string planted = """
            using static System.DateTime;
            using Clock = System.DateTimeOffset;
            var a = UtcNow;
            var b = Clock.UtcNow;
            var c = DateTime.Now;
            """;

        DirectClockScan.Offenders([("Planted.cs", planted)]).Should().BeEquivalentTo("Planted.cs: DateTime.Now");
    }

    /// <summary>A clock read named inside a string literal is text, not a read.</summary>
    [Fact]
    public void Rule_IgnoresReadsNamedInsideStrings()
    {
        const string benign = """
            var a = "DateTime.Now";
            var b = $"{time.GetUtcNow()} instead of DateTimeOffset.UtcNow";
            """;

        DirectClockScan.Offenders([("Benign.cs", benign)]).Should().BeEmpty();
    }
}
