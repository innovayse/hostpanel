namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// No emoji in anything a person reads from the backend: the resource files, the email and notification templates, the
/// seeded texts and every message built in code. A pictograph is an icon, and an icon comes from the icon library of the
/// interface, never from a text.
/// </summary>
public sealed class NoEmojiTests
{
    /// <summary>The kinds of file whose text reaches a person.</summary>
    private static readonly string[] TextExtensions = [".cs", ".resx", ".html", ".htm", ".json", ".txt", ".liquid", ".hbs", ".sql"];

    /// <summary>The solution directory.</summary>
    private static string BackendRoot => BackendLayout.FindRoot(AppContext.BaseDirectory);

    /// <summary>Every authored text file under <c>backend/src</c> as a path relative to it and its text; build output is not authored.</summary>
    /// <returns>The texts.</returns>
    private static List<(string Name, string Text)> Sources()
    {
        var src = Path.Combine(BackendRoot, "src");
        return Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories)
            .Where(path => TextExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Select(path => (Name: Path.GetRelativePath(src, path).Replace('\\', '/'), Path: path))
            .Where(file => !file.Name.Contains("/obj/", StringComparison.Ordinal)
                && !file.Name.Contains("/bin/", StringComparison.Ordinal)
                && !file.Name.Contains("/node_modules/", StringComparison.Ordinal))
            .Select(file => (file.Name, File.ReadAllText(file.Path)))
            .ToList();
    }

    /// <summary>No source file, resource file or template holds an emoji.</summary>
    [Fact]
    public void Sources_HoldNoEmoji()
    {
        EmojiScan.Offenders(Sources()).Should().BeEmpty("a pictograph in a text is an ad hoc icon; say it in words");
    }

    /// <summary>The scan reads the resource files and the templates, so the rule is proven to cover them.</summary>
    [Fact]
    public void Scan_ReadsResourceFiles()
    {
        Sources().Select(source => source.Name).Should().Contain(name => name.EndsWith(".resx", StringComparison.Ordinal));
    }

    /// <summary>A planted emoji, a symbol, a dingbat and a variation selector are all reported, so the rule is proven to bite.</summary>
    [Fact]
    public void Rule_FailsOnPlantedEmoji()
    {
        var offenders = EmojiScan.Offenders(
            [
                ("Planted/Bell.resx", "New message \U0001F514"),
                ("Planted/Star.cs", "⭐ Popular"),
                ("Planted/Check.html", "<b>✔</b> done"),
                ("Planted/Selector.txt", "heart️"),
                ("Planted/Clean.resx", "Plain text, arrows -> and · dots, Հայերեն"),
            ]);

        offenders.Should().Equal("Planted/Bell.resx", "Planted/Star.cs", "Planted/Check.html", "Planted/Selector.txt");
    }
}
