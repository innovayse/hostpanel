using Innovayse.Hostpanel.Infrastructure.Plugins;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// Which plugin, if any, already ships an assembly file name a package carries: another installed, staged or
/// built-in plugin conflicts (whatever the case), the plugin's own folders never do. Real folders in a temp
/// directory; no container or database.
/// </summary>
public sealed class PluginAssemblyConflictsTests : IDisposable
{
    /// <summary>The temp folder holding the fake upload and built-in roots of one test.</summary>
    private readonly string _base = Path.Combine(Path.GetTempPath(), $"hostpanel-asmconflict-{Guid.NewGuid():N}");

    /// <summary>The fake upload folder.</summary>
    private string InstallRoot => Path.Combine(_base, "uploads");

    /// <summary>The fake folder of the plugins shipped with the build.</summary>
    private string BuiltInRoot => Path.Combine(_base, "builtin");

    /// <summary>Deletes the temp folder.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_base))
        {
            Directory.Delete(_base, recursive: true);
        }
    }

    /// <summary>Writes an empty file, creating its folders.</summary>
    /// <param name="path">The absolute file path.</param>
    private static void Touch(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
    }

    /// <summary>The same assembly file name in another installed plugin is a conflict that names that plugin.</summary>
    [Fact]
    public void AnAssemblyOfAnotherInstalledPlugin_Conflicts()
    {
        Touch(Path.Combine(InstallRoot, "plugin-a", "Shared.Lib.dll"));

        var found = PluginAssemblyConflicts.Find("plugin-b", ["Shared.Lib.dll"], InstallRoot, BuiltInRoot);

        found.Should().Be("plugin-a");
    }

    /// <summary>The comparison ignores case.</summary>
    [Fact]
    public void ADifferentCase_StillConflicts()
    {
        Touch(Path.Combine(InstallRoot, "plugin-a", "SHARED.lib.DLL"));

        var found = PluginAssemblyConflicts.Find("plugin-b", ["shared.LIB.dll"], InstallRoot, BuiltInRoot);

        found.Should().Be("plugin-a");
    }

    /// <summary>An upgrade staged for another plugin counts too.</summary>
    [Fact]
    public void AnAssemblyOfAnotherStagedPlugin_Conflicts()
    {
        Touch(Path.Combine(InstallRoot, ".staged", "plugin-a", "Shared.Lib.dll"));

        var found = PluginAssemblyConflicts.Find("plugin-b", ["Shared.Lib.dll"], InstallRoot, BuiltInRoot);

        found.Should().Be("plugin-a");
    }

    /// <summary>A plugin shipped with the build counts too.</summary>
    [Fact]
    public void AnAssemblyOfABuiltInPlugin_Conflicts()
    {
        Touch(Path.Combine(BuiltInRoot, "built-in", "Shared.Lib.dll"));

        var found = PluginAssemblyConflicts.Find("plugin-b", ["Shared.Lib.dll"], InstallRoot, BuiltInRoot);

        found.Should().Be("built-in");
    }

    /// <summary>Upgrading a plugin never conflicts with its own installed or staged folder.</summary>
    [Fact]
    public void TheSamePluginId_DoesNotConflictWithItself()
    {
        Touch(Path.Combine(InstallRoot, "plugin-a", "Shared.Lib.dll"));
        Touch(Path.Combine(InstallRoot, ".staged", "plugin-a", "Shared.Lib.dll"));

        var found = PluginAssemblyConflicts.Find("plugin-a", ["Shared.Lib.dll"], InstallRoot, BuiltInRoot);

        found.Should().BeNull();
    }

    /// <summary>Distinct assembly names never conflict, and a non-assembly file of the same name is ignored.</summary>
    [Fact]
    public void DistinctNames_DoNotConflict()
    {
        Touch(Path.Combine(InstallRoot, "plugin-a", "Other.Lib.dll"));
        Touch(Path.Combine(InstallRoot, "plugin-a", "Shared.Lib.txt"));

        var found = PluginAssemblyConflicts.Find("plugin-b", ["Shared.Lib.dll"], InstallRoot, BuiltInRoot);

        found.Should().BeNull();
    }

    /// <summary>With no folders on disk there is nothing to conflict with.</summary>
    [Fact]
    public void MissingRoots_DoNotConflict()
    {
        PluginAssemblyConflicts.Find("plugin-b", ["Shared.Lib.dll"], InstallRoot, BuiltInRoot).Should().BeNull();
    }
}
