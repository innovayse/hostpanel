namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Pdf;

/// <summary>
/// The licence texts of the bundled invoice fonts must ship with the application. The project
/// declares them as copied content, so build and publish both carry them to <c>Pdf/Fonts/</c>;
/// this test proves the build output holds every <c>LICENSE-*.txt</c> of the source folder,
/// unchanged and recognisable as a licence.
/// </summary>
public sealed class FontLicenceTests
{
    /// <summary>The line every licence text carries when no characteristic line is registered for it.</summary>
    private const string GenericMarker = "Permission is hereby granted";

    /// <summary>A characteristic line of each known licence, by file name.</summary>
    private static readonly Dictionary<string, string> Markers = new()
    {
        ["LICENSE-DejaVu.txt"] = "Bitstream Vera Fonts Copyright",
        ["LICENSE-NotoSansArmenian.txt"] = "SIL OPEN FONT LICENSE Version 1.1",
    };

    /// <summary>The licence files of the source folder, so a newly added licence is checked too.</summary>
    public static TheoryData<string> LicenceFiles { get; } = ReadSourceLicenceNames();

    /// <summary>A licence file is present in the output, equals its source and names its licence.</summary>
    /// <param name="fileName">The licence file name.</param>
    [Theory]
    [MemberData(nameof(LicenceFiles))]
    public void BuildOutput_ContainsFontLicence(string fileName)
    {
        // The test host runs from the build output, and the licences are copied content, so
        // AppContext.BaseDirectory is exactly where the build (and publish) puts them.
        var path = Path.Combine(AppContext.BaseDirectory, "Pdf", "Fonts", fileName);

        File.Exists(path).Should().BeTrue($"{fileName} must be copied to the output next to the fonts");

        var text = File.ReadAllText(path);
        text.Should().Be(File.ReadAllText(Path.Combine(SourceFontsFolder(), fileName)));
        text.Should().Contain(Markers.GetValueOrDefault(fileName, GenericMarker));
    }

    /// <summary>The source folder holds at least one licence, so the theory above cannot pass by checking nothing.</summary>
    [Fact]
    public void SourceFolder_HoldsLicences()
    {
        LicenceFiles.Should().NotBeEmpty();
    }

    /// <summary>Lists the licence file names in the source fonts folder.</summary>
    /// <returns>One row per <c>LICENSE-*.txt</c>.</returns>
    private static TheoryData<string> ReadSourceLicenceNames()
    {
        var data = new TheoryData<string>();

        foreach (var file in Directory.GetFiles(SourceFontsFolder(), "LICENSE-*.txt").OrderBy(f => f, StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(file));
        }

        return data;
    }

    /// <summary>Finds the fonts folder in the Infrastructure source by walking up to the solution folder.</summary>
    /// <returns>The absolute path of <c>Pdf/Fonts</c> under the Infrastructure project.</returns>
    private static string SourceFontsFolder()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (current.GetFiles("*.sln").Length > 0)
            {
                return Path.Combine(current.FullName, "src", "Innovayse.Hostpanel.Infrastructure", "Pdf", "Fonts");
            }

            current = current.Parent;
        }

        throw new InvalidOperationException($"No solution file found walking up from '{AppContext.BaseDirectory}'.");
    }
}
