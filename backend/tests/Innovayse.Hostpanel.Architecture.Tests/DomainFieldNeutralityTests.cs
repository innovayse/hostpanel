namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// The host holds nothing specific to a zone or a country in what it does with the extra fields of a domain. Which fields a
/// registry asks for, for which zones, comes from the registrar plugin's manifest; the code, the tests and the texts of this
/// feature name no extension, no country code and no national document. A test that needs a zone invents one.
/// </summary>
public sealed class DomainFieldNeutralityTests
{
    /// <summary>The marks in a file name that put the file in the feature: the domain field use cases and the stored extra values.</summary>
    private static readonly string[] FeatureNameMarks = ["DomainField", "ExtendedAttributes"];

    /// <summary>The solution directory.</summary>
    private static string BackendRoot => UseCaseShapeTests.FindBackendRoot(AppContext.BaseDirectory);

    /// <summary>Whether a path is a file this rule reads: authored C# of the feature, never build output, a migration or the rule itself.</summary>
    /// <param name="path">The file's path.</param>
    /// <returns><see langword="true"/> when the feature owns the file and the rule applies to it.</returns>
    private static bool IsFeatureFile(string path)
    {
        var normalized = path.Replace('\\', '/');
        var name = Path.GetFileName(path);
        return !normalized.Contains("/obj/", StringComparison.Ordinal)
            && !normalized.Contains("/bin/", StringComparison.Ordinal)
            && !normalized.Contains("/Migrations/", StringComparison.Ordinal)
            && !normalized.Contains("/Hostpanel.Sdk/", StringComparison.Ordinal)
            && !name.StartsWith(nameof(DomainFieldNeutralityTests), StringComparison.Ordinal)
            && !name.StartsWith(nameof(DomainFieldNeutralityScan), StringComparison.Ordinal)
            && FeatureNameMarks.Any(mark => name.Contains(mark, StringComparison.Ordinal));
    }

    /// <summary>The feature's source and test files as a path relative to the backend and the text.</summary>
    /// <returns>The sources.</returns>
    private static List<(string Name, string Text)> FeatureSources()
    {
        return new[] { "src", "tests" }
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(BackendRoot, folder), "*.cs", SearchOption.AllDirectories))
            .Where(IsFeatureFile)
            .Select(path => (Name: Path.GetRelativePath(BackendRoot, path).Replace('\\', '/'), Text: File.ReadAllText(path)))
            .ToList();
    }

    /// <summary>The scan finds the feature's files; a rule over none would pass for the wrong reason.</summary>
    [Fact]
    public void FeatureFiles_AreFound()
    {
        var names = FeatureSources().Select(source => source.Name).ToList();

        names.Should().Contain(name => name.EndsWith("Domains/DomainExtendedAttributes.cs", StringComparison.Ordinal));
        names.Should().Contain(name => name.EndsWith("Domains/Registrar/DomainFieldsValidation.cs", StringComparison.Ordinal));
        names.Should().Contain(name => name.EndsWith("Domains/DomainFieldsValidationTests.cs", StringComparison.Ordinal));
    }

    /// <summary>No file of the feature names a zone, a country or a national document.</summary>
    [Fact]
    public void FeatureSources_NameNoZoneNoCountryAndNoNationalDocument()
    {
        var offenders = DomainFieldNeutralityScan.Offenders(FeatureSources());

        offenders.Should().BeEmpty("the fields a registry asks for come from the plugin manifest, and a test invents its zone");
    }

    /// <summary>A planted extension, country code and national document are all reported, so the rule is proven to bite.</summary>
    [Fact]
    public void Rule_FailsOnPlantedZonesAndDocuments()
    {
        var offenders = DomainFieldNeutralityScan.Offenders(
            [
                ("Planted/Bare.cs", "var zone = \"" + "xx" + "\";"),
                ("Planted/Dotted.cs", "var name = \"example." + "xx" + "\";"),
                ("Planted/Generic.cs", "var name = \"example." + "com" + "\";"),
                ("Planted/Punycode.cs", "var zone = \"xn--" + "abc" + "\";"),
                ("Planted/Document.cs", "// asks for the Pass" + "port number"),
            ]);

        offenders.Select(offender => offender.Split(" -> ")[0])
            .Should().Equal("Planted/Bare.cs", "Planted/Dotted.cs", "Planted/Generic.cs", "Planted/Punycode.cs", "Planted/Document.cs");
    }

    /// <summary>An invented zone, a language code held in a constant, a country written as a constant and a plain key are not hits.</summary>
    [Fact]
    public void Rule_IgnoresInventedZonesAndPlainText()
    {
        DomainFieldNeutralityScan.HitsIn(
                "var name = \"name.alpha\"; var key = \"holder-number\"; var path = \"/api/domains/fields?domain=a.b\"; var pattern = \"[A-Z]{2}\";")
            .Should().BeEmpty();
    }
}
