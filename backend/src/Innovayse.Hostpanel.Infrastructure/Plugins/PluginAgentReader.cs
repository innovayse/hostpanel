using Hostpanel.Sdk.Packaging.Agent;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Reads and checks the SSH key agent files a plugin manifest names a profile for
/// (<see cref="PluginManifest.SshAgentProfile"/>), once, for both the startup load and the install. The rules (a small
/// profile in the agent's configuration format that sets only keys the agent defines, plainly named helpers within a count
/// and size cap, no link on the way) are the plugin packaging package's <see cref="PluginAgentPackageChecker"/>, the code a
/// plugin author runs before publishing; this adapter only turns the answer into the host's
/// <see cref="PluginAgentProfileDto"/> and violation. The bytes are kept in memory, so building an installer bundle reads
/// no file and installs exactly what was checked.
/// </summary>
/// <remarks>
/// Every refusal detail names a package-relative path or a profile key only: an absolute host path or an exception message
/// never reaches a log line or an install response.
/// </remarks>
internal static class PluginAgentReader
{
    /// <summary>Reads the agent files the manifest names a profile for, if any, and checks them against the agent rules.</summary>
    /// <param name="pluginDirectory">The plugin folder: the installed folder at load, the unpacked staging folder at install.</param>
    /// <param name="manifest">The plugin's manifest.</param>
    /// <returns>The checked profile, nothing when the manifest names none, or the first rule the files break.</returns>
    public static PluginAgentRead Read(string pluginDirectory, PluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var check = PluginAgentPackageChecker.Check(pluginDirectory, manifest.SshAgentProfile);
        if (check.Violation is not null)
        {
            return new PluginAgentRead(null, PluginRuleMap.ToHost(check.Violation));
        }

        var profile = check.Files is null
            ? null
            : new PluginAgentProfileDto(check.Files.ProfileId, manifest.Id, check.Files.Profile, check.Files.Helpers);
        return new PluginAgentRead(profile, null);
    }
}
