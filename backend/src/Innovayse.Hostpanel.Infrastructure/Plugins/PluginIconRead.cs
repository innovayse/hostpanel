using Innovayse.Hostpanel.Application.Admin.Plugins.Common;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>The outcome of reading the icon a plugin manifest names.</summary>
/// <param name="Icon">The checked icon; <see langword="null"/> when the manifest names none, or when a rule is broken.</param>
/// <param name="Violation">The icon rule the plugin breaks, or <see langword="null"/> when the icon is absent or valid.</param>
internal sealed record PluginIconRead(PluginIconDto? Icon, PluginManifestViolation? Violation);
