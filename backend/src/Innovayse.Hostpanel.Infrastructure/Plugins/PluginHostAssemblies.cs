using Hostpanel.Sdk.Packaging.Naming;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The host's side of the reserved assembly names: the assemblies the running process provides (the runtime's trusted
/// platform assemblies, which cover the framework and the host's own dependencies) handed to the packaging package's
/// <see cref="PluginAssemblyNames"/>, which holds the naming rule itself. The one place the host decides what it provides.
/// </summary>
internal static class PluginHostAssemblies
{
    /// <summary>The prefix of every host assembly; the boundary of the names a plugin package may not carry.</summary>
    private const string HostPrefix = "Innovayse.Hostpanel.";

    /// <summary>The one namespace under <see cref="HostPrefix"/> that belongs to plugins; no host assembly may be named under it.</summary>
    private const string PluginNamespacePrefix = "Innovayse.Hostpanel.Plugins.";

    /// <summary>The former name of the plugin SDK, which a plugin package may still not ship.</summary>
    private const string FormerSdkName = "Innovayse.SDK";

    /// <summary>
    /// How the host names its own assemblies, handed to every assembly name check. The one place these three values are
    /// written; the architecture test of the plugin namespace reads <see cref="Options"/> too.
    /// </summary>
    public static PluginAssemblyOptions Options { get; } = new(HostPrefix, PluginNamespacePrefix)
    {
        ExtraReservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { FormerSdkName },
    };

    /// <summary>The simple names of the runtime's trusted platform assemblies of the running process, compared ignoring case.</summary>
    private static readonly Lazy<HashSet<string>> PlatformAssemblies = new(LoadPlatformAssemblies);

    /// <summary>Whether an archive entry is an assembly the running host already provides.</summary>
    /// <param name="entryFileName">The entry's file name, without its folder.</param>
    /// <returns><see langword="true"/> for a <c>.dll</c> or <c>.exe</c> named like a host, SDK or framework assembly.</returns>
    public static bool IsProvided(string entryFileName)
    {
        return PluginAssemblyNames.IsReserved(entryFileName, PlatformAssemblies.Value, Options);
    }

    /// <summary>Reads the runtime's trusted platform assembly list into a set of simple names.</summary>
    /// <returns>The names, compared case-insensitively.</returns>
    private static HashSet<string> LoadPlatformAssemblies()
    {
        var list = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty;
        return list
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
