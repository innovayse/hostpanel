namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Finds an assembly file name that a package shares with another plugin. A plugin folder is loaded with
/// <c>Assembly.LoadFrom</c>, which hands back the copy already loaded for a name, so two plugins shipping the same DLL
/// name would silently run on one plugin's copy. Folders of the plugin being installed (its installed and its staged
/// folder) are skipped, so an upgrade never conflicts with itself.
/// </summary>
internal static class PluginAssemblyConflicts
{
    /// <summary>Finds the plugin whose assembly file name equals one of the package's, ignoring case.</summary>
    /// <param name="pluginId">The id of the plugin being installed; its own folders are not compared.</param>
    /// <param name="assemblyFileNames">The file names of the assemblies (<c>.dll</c> and <c>.exe</c>) the package carries, without folders.</param>
    /// <param name="installRoot">The upload folder, holding the uploaded plugins and, under <see cref="PluginFolders.Staged"/>, the staged ones.</param>
    /// <param name="builtInRoot">The folder of the plugins shipped with the build.</param>
    /// <returns>The id of the first conflicting plugin; <see langword="null"/> when none conflicts.</returns>
    public static string? Find(string pluginId, IReadOnlyCollection<string> assemblyFileNames, string installRoot, string builtInRoot)
    {
        ArgumentNullException.ThrowIfNull(assemblyFileNames);

        if (assemblyFileNames.Count == 0)
        {
            return null;
        }

        var names = new HashSet<string>(assemblyFileNames, StringComparer.OrdinalIgnoreCase);
        var roots = new[] { builtInRoot, installRoot, Path.Combine(installRoot, PluginFolders.Staged) };
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var folder in Directory.EnumerateDirectories(root).Order(StringComparer.OrdinalIgnoreCase))
            {
                var otherId = Path.GetFileName(folder);

                // Working folders (.staged, .upgrades, .staging) start with a dot and are not plugins.
                if (otherId.StartsWith('.') || otherId.Equals(pluginId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                    .Any(file => IsAssembly(file) && names.Contains(Path.GetFileName(file))))
                {
                    return otherId;
                }
            }
        }

        return null;
    }

    /// <summary>Whether a file is an assembly the loader would take, by extension.</summary>
    /// <param name="fileName">A file name or path.</param>
    /// <returns><see langword="true"/> for a <c>.dll</c> or <c>.exe</c>.</returns>
    public static bool IsAssembly(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return extension.Equals(".dll", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".exe", StringComparison.OrdinalIgnoreCase);
    }
}
