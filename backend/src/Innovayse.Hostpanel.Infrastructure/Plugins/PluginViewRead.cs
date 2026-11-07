using Innovayse.Hostpanel.Application.Plugins.Common;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>The outcome of reading the view documents a plugin's pages and slots name.</summary>
/// <param name="Views">
/// The documents by package-relative path; <see cref="PluginViewDocuments.None"/> when a rule is broken.
/// </param>
/// <param name="Violation">
/// The first view rule the plugin breaks and that refuses it at install and at load, or <see langword="null"/>.
/// </param>
/// <param name="Advisories">
/// The faults of rules added after plugins were installed (a block id given to two blocks, a mistyped display member),
/// in document order. They refuse a plugin at install only; at load they are warned about and the plugin stays.
/// </param>
internal sealed record PluginViewRead(
    PluginViewDocuments Views, PluginManifestViolation? Violation, IReadOnlyList<PluginManifestViolation> Advisories)
{
    /// <summary>
    /// Gets what refuses the plugin at install: a hard violation first, else the first advisory. An upload is new text
    /// the operator has not run yet, so every rule applies; a plugin that is already installed is judged by
    /// <see cref="Violation"/> alone.
    /// </summary>
    public PluginManifestViolation? RefusedAtInstall => Violation ?? Advisories.FirstOrDefault();
}
