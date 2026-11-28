namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// The core source names no plugin. No Domain, Application, Infrastructure, API, Shared or SharedKernel file may
/// contain the id or entry point type of a plugin that lives outside this repository
/// (<see cref="PluginNeutralSourceScan.PluginNames"/>). The host admin and client interfaces are held to the same
/// rule by their own vitest suites (<c>pluginNeutrality.test.ts</c>).
/// </summary>
public sealed class PluginNeutralSourceTests
{
    /// <summary>The core projects under <c>backend/src</c> the rule covers.</summary>
    private static readonly string[] CoreProjects =
    [
        "Innovayse.Hostpanel.Domain", "Innovayse.Hostpanel.Application", "Innovayse.Hostpanel.Infrastructure",
        "Innovayse.Hostpanel.API", "Shared", "SharedKernel",
    ];

    /// <summary>The solution directory.</summary>
    private static string BackendRoot => BackendLayout.FindRoot(AppContext.BaseDirectory);

    /// <summary>Every core C# source file as a path relative to <c>backend/src</c> and its text; build output and migrations are not authored code.</summary>
    /// <returns>The sources.</returns>
    private static List<(string Name, string Text)> CoreSources()
    {
        var src = Path.Combine(BackendRoot, "src");
        return CoreProjects
            .SelectMany(project => Directory.EnumerateFiles(Path.Combine(src, project), "*.cs", SearchOption.AllDirectories))
            .Select(path => (Name: Path.GetRelativePath(src, path).Replace('\\', '/'), Path: path))
            .Where(file => !file.Name.Contains("/obj/", StringComparison.Ordinal)
                && !file.Name.Contains("/bin/", StringComparison.Ordinal)
                && !file.Name.Contains("/Migrations/", StringComparison.Ordinal))
            .Select(file => (file.Name, File.ReadAllText(file.Path)))
            .ToList();
    }

    /// <summary>No core file names a plugin id or plugin type.</summary>
    [Fact]
    public void CoreSources_NameNoPlugin()
    {
        var offenders = PluginNeutralSourceScan.Offenders(CoreSources(), PluginNeutralSourceScan.PluginNames);

        offenders.Should().BeEmpty("a plugin is reached through its manifest and an SDK contract, never named by the core");
    }

    /// <summary>A planted plugin id and a planted plugin type name in an in-memory source are both reported, so the rule is proven to bite.</summary>
    [Fact]
    public void Rule_FailsOnPlantedReference()
    {
        var offenders = PluginNeutralSourceScan.Offenders(
            [
                ("Planted/Id.cs", "const string Id = \"innovayse-cwp\";"),
                ("Planted/Type.cs", "new CwpProvisioningProvider();"),
                ("Planted/Clean.cs", "const string Id = \"other\";"),
            ],
            PluginNeutralSourceScan.PluginNames);

        offenders.Should().Equal("Planted/Id.cs -> innovayse-cwp", "Planted/Type.cs -> CwpProvisioningProvider");
    }

    /// <summary>A name that is only part of a longer identifier or id is not a reference.</summary>
    [Fact]
    public void Rule_IgnoresANameInsideALongerToken()
    {
        PluginNeutralSourceScan.NamesIn("var x = paypalSuffix; var y = Prefixpaypal; var z = \"paypal-more\";", PluginNeutralSourceScan.PluginNames)
            .Should().NotContain("paypal");
    }
}
