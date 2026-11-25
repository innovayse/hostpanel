using System.Reflection;
using System.Runtime.Loader;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The load context one plugin's assemblies live in. It is collectible, so unloading it, once nothing holds a type or an
/// instance from it, frees the plugin's code and lets a newer version of the same files load in a context of its own.
/// </summary>
/// <remarks>
/// <para>
/// An assembly that is not in the plugin's folder is answered with <see langword="null"/>, which makes the runtime resolve it
/// in the default context. That is how the plugin shares the host's SDK and framework assemblies, so a plugin's
/// <c>IPaymentPlugin</c> is the very type the host casts to; a package never carries its own copy of them
/// (<c>PLUGIN_HOST_ASSEMBLY_REFUSED</c>).
/// </para>
/// <para>
/// Assemblies are loaded from the bytes of the file, never from its path. The runtime remembers the images it loaded by path,
/// so a newer version written to the same path would come back as the old code while the old context has not been collected
/// yet; and the plugin's files stay free to be replaced while it runs.
/// </para>
/// </remarks>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    /// <summary>The plugin's folder, where its own assemblies and their dependencies are.</summary>
    private readonly string _folder;

    /// <summary>Initializes a collectible context for one plugin folder.</summary>
    /// <param name="pluginId">The plugin id, which names the context in diagnostics.</param>
    /// <param name="folder">The plugin's folder.</param>
    public PluginLoadContext(string pluginId, string folder)
        : base($"plugin:{pluginId}", isCollectible: true)
    {
        _folder = folder;
    }

    /// <summary>Loads one assembly file of the plugin into this context.</summary>
    /// <param name="path">The assembly file, in the plugin's folder.</param>
    /// <returns>The loaded assembly.</returns>
    public Assembly LoadFromFile(string path)
    {
        using var stream = new MemoryStream(File.ReadAllBytes(path));
        return LoadFromStream(stream);
    }

    /// <summary>Loads a dependency from the plugin's folder when the folder holds it.</summary>
    /// <param name="assemblyName">The assembly the runtime is resolving.</param>
    /// <returns>The assembly from the plugin's folder; <see langword="null"/> to let the default context resolve it.</returns>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = Path.Combine(_folder, assemblyName.Name + ".dll");
        return File.Exists(path) ? LoadFromFile(path) : null;
    }
}
