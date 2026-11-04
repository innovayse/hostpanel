using System.Text.Json;
using System.Text.RegularExpressions;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// Finds the plugin ids and plugin type names that core source text names. The names are read from the
/// <c>plugin.json</c> manifests under <c>backend/src/Innovayse.Providers.*</c> (the id and the simple name of the
/// entry point type), so a new in-tree plugin is covered without touching a list. The rule is a plain function
/// of named sources and names, so a test can run it on an in-memory planted source and on the real files.
/// </summary>
internal static class PluginNeutralSourceScan
{
    /// <summary>The plugin ids and entry point type names every in-tree plugin manifest declares.</summary>
    /// <param name="backendRoot">The directory that holds the solution file.</param>
    /// <returns>The distinct names, each a whole token a core source must not contain.</returns>
    public static IReadOnlyList<string> PluginNames(string backendRoot)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        var providers = Directory.EnumerateDirectories(Path.Combine(backendRoot, "src"), "Innovayse.Providers.*");
        foreach (var manifest in providers.Select(directory => Path.Combine(directory, "plugin.json")).Where(File.Exists))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifest));
            var root = document.RootElement;
            if (root.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } pluginId)
            {
                names.Add(pluginId);
            }

            if (root.TryGetProperty("entryPoint", out var entry) && entry.GetString() is { Length: > 0 } entryPoint)
            {
                names.Add(entryPoint[(entryPoint.LastIndexOf('.') + 1)..]);
            }
        }

        return [.. names];
    }

    /// <summary>The names a text contains as whole tokens. Matching is case sensitive and a name inside a longer identifier or id does not count.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="names">The plugin ids and type names.</param>
    /// <returns>The names found, in the order of <paramref name="names"/>.</returns>
    public static IReadOnlyList<string> NamesIn(string text, IReadOnlyList<string> names)
    {
        return names
            .Where(name => Regex.IsMatch(text, $"(?<![A-Za-z0-9-]){Regex.Escape(name)}(?![A-Za-z0-9-])", RegexOptions.CultureInvariant))
            .ToList();
    }

    /// <summary>The sources that name a plugin.</summary>
    /// <param name="sources">Pairs of a name for messages and the source text.</param>
    /// <param name="names">The plugin ids and type names.</param>
    /// <returns>One <c>name -> plugin name</c> line per hit.</returns>
    public static IReadOnlyList<string> Offenders(IEnumerable<(string Name, string Text)> sources, IReadOnlyList<string> names)
    {
        return sources
            .SelectMany(source => NamesIn(source.Text, names).Select(hit => $"{source.Name} -> {hit}"))
            .ToList();
    }
}
