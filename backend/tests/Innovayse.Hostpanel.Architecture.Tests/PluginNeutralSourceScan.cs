using System.Text.RegularExpressions;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// Finds the plugin ids and plugin type names that core source text names. The names are a fixed deny list of
/// plugins that live outside this repository; the rule is a plain function of named sources and names, so a test
/// can run it on an in-memory planted source and on the real files.
/// </summary>
internal static class PluginNeutralSourceScan
{
    /// <summary>
    /// The ids and entry point type names of plugins that live in their own repositories. The core must never name
    /// them: a plugin is reached through its manifest and an SDK contract. The list grows when a plugin moves out of the tree.
    /// </summary>
    public static readonly IReadOnlyList<string> PluginNames =
    [
        "inecobank", "InecobankPaymentGateway",
        "paypal", "PayPalPaymentGateway",
        "idram", "IdramPaymentGateway",
        "ameriabank", "AmeriabankPaymentGateway",
        "innovayse-cwp", "CwpProvisioningProvider", "CwpApiClient",
    ];

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
