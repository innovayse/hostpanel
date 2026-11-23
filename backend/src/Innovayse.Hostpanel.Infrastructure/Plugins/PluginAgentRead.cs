using Innovayse.Hostpanel.Application.Admin.Plugins.Common;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>The outcome of reading the SSH key agent files a plugin manifest names a profile for.</summary>
/// <param name="Profile">The checked profile and helpers; <see langword="null"/> when the manifest names none, or when a rule is broken.</param>
/// <param name="Violation">The agent rule the package breaks, or <see langword="null"/> when the files are absent or valid.</param>
internal sealed record PluginAgentRead(PluginAgentProfileDto? Profile, PluginManifestViolation? Violation);
