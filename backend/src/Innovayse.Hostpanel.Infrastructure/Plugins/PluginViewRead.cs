using Innovayse.Hostpanel.Application.Plugins.Common;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>The outcome of reading the view documents a plugin's pages and slots name.</summary>
/// <param name="Views">The documents by package-relative path; <see cref="PluginViewDocuments.None"/> when a rule is broken.</param>
/// <param name="Violation">The first view rule the plugin breaks, or <see langword="null"/> when every document is valid.</param>
internal sealed record PluginViewRead(PluginViewDocuments Views, PluginManifestViolation? Violation);
