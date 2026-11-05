namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// The core source names no plugin. The plugin ids and entry point type names come from the in-tree
/// <c>plugin.json</c> manifests (<see cref="PluginNeutralSourceScan"/>); no Domain, Application, Infrastructure,
/// API, Shared or SharedKernel file may contain one, apart from <see cref="LegacyBuiltIn"/>. The host admin and
/// client interfaces are held to the same rule by their own vitest suites (<c>pluginNeutrality.test.ts</c>).
/// </summary>
public sealed class PluginNeutralSourceTests
{
    /// <summary>The core projects under <c>backend/src</c> the rule covers.</summary>
    private static readonly string[] CoreProjects =
    [
        "Innovayse.Hostpanel.Domain", "Innovayse.Hostpanel.Application", "Innovayse.Hostpanel.Infrastructure",
        "Innovayse.Hostpanel.API", "Shared", "SharedKernel",
    ];

    /// <summary>
    /// The files that still name a built-in provider: the integrations slugs, the dependency registration and the
    /// provisioning factory that links the panel provider. This list only shrinks; a new entry needs a reason, and an
    /// entry whose file no longer names a plugin fails <see cref="LegacyBuiltIn_ListHasNoStaleEntry"/>.
    /// </summary>
    private static readonly string[] LegacyBuiltIn =
    [
        "Innovayse.Hostpanel.Domain/Settings/IntegrationSlugs.cs",
        "Innovayse.Hostpanel.Infrastructure/DependencyInjection.cs",
        "Innovayse.Hostpanel.Infrastructure/Provisioning/ProvisioningProviderFactory.cs",
    ];

    /// <summary>The solution directory.</summary>
    private static string BackendRoot => UseCaseShapeTests.FindBackendRoot(AppContext.BaseDirectory);

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

    /// <summary>The manifests of the in-tree plugins yield names to look for.</summary>
    [Fact]
    public void PluginNames_AreReadFromTheManifests()
    {
        PluginNeutralSourceScan.PluginNames(BackendRoot).Should().NotBeEmpty("the in-tree plugins declare an id and an entry point");
    }

    /// <summary>No core file outside the legacy list names a plugin id or plugin type.</summary>
    [Fact]
    public void CoreSources_NameNoPlugin()
    {
        var names = PluginNeutralSourceScan.PluginNames(BackendRoot);

        var offenders = PluginNeutralSourceScan.Offenders(CoreSources().Where(source => !LegacyBuiltIn.Contains(source.Name)), names);

        offenders.Should().BeEmpty("a plugin is reached through its manifest and an SDK contract, never named by the core");
    }

    /// <summary>Every legacy entry still names a plugin, so the list shrinks as the references go.</summary>
    [Fact]
    public void LegacyBuiltIn_ListHasNoStaleEntry()
    {
        var names = PluginNeutralSourceScan.PluginNames(BackendRoot);
        var sources = CoreSources();

        foreach (var entry in LegacyBuiltIn)
        {
            sources.Should().Contain(source => source.Name == entry, "the legacy entry must be an existing core file");
            PluginNeutralSourceScan.NamesIn(sources.Single(source => source.Name == entry).Text, names)
                .Should().NotBeEmpty($"{entry} no longer names a plugin, so remove it from the legacy list");
        }
    }

    /// <summary>A planted plugin id and a planted plugin type name in an in-memory source are both reported, so the rule is proven to bite.</summary>
    [Fact]
    public void Rule_FailsOnPlantedReference()
    {
        var names = PluginNeutralSourceScan.PluginNames(BackendRoot);
        var id = names.First(name => char.IsLower(name[0]));
        var type = names.First(name => char.IsUpper(name[0]));

        var offenders = PluginNeutralSourceScan.Offenders(
            [("Planted/Id.cs", $"const string Id = \"{id}\";"), ("Planted/Type.cs", $"new {type}();"), ("Planted/Clean.cs", "const string Id = \"other\";")],
            names);

        offenders.Should().Equal($"Planted/Id.cs -> {id}", $"Planted/Type.cs -> {type}");
    }

    /// <summary>A name that is only part of a longer identifier or id is not a reference.</summary>
    [Fact]
    public void Rule_IgnoresANameInsideALongerToken()
    {
        var names = PluginNeutralSourceScan.PluginNames(BackendRoot);
        var name = names[0];

        PluginNeutralSourceScan.NamesIn($"var x = {name}Suffix; var y = Prefix{name}; var z = \"{name}-more\";", names)
            .Should().NotContain(name);
    }
}
