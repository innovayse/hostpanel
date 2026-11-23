namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// The SSH key agent profile a loaded plugin ships, read and checked when the plugin was loaded (see the agent rules of the
/// package check), so building an installer bundle reads no file.
/// </summary>
/// <param name="ProfileId">The profile id the manifest names; it is also the <c>provider</c> the profile sets.</param>
/// <param name="PluginId">The plugin that ships the profile.</param>
/// <param name="Profile">The checked bytes of the profile file.</param>
/// <param name="Helpers">The checked bytes of each helper by file name, in ordinal order; empty when the plugin ships none.</param>
public sealed record PluginAgentProfileDto(
    string ProfileId, string PluginId, byte[] Profile, IReadOnlyDictionary<string, byte[]> Helpers);
