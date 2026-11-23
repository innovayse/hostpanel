using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent;

/// <summary>
/// <see cref="ISshAgentBundles"/> over the agent files embedded in this assembly (<c>backend/agent/ssh-key-agent</c>, tests
/// excluded) and the profiles the loaded plugins ship. The built in bundle is packed once; the bundle of a plugin's profile
/// is the same files plus <c>profiles/&lt;id&gt;.conf</c> and <c>providers/&lt;id&gt;/&lt;helper&gt;</c>, packed from the
/// bytes that were checked at load (once per profile, kept for the life of the process because the loaded plugins do not
/// change until the next start), so a request reads no file. Nothing here names a plugin or a panel: the profile id and
/// the helpers all come from the plugin's package.
/// </summary>
/// <remarks>
/// Public, like every adapter the container registers: Wolverine builds the handlers that take it with plain constructor calls.
/// </remarks>
public sealed partial class SshAgentBundles : ISshAgentBundles
{
    /// <summary>Prefix of the agent files' manifest resource names.</summary>
    private const string ResourcePrefix = "SshAgent/";

    /// <summary>The path of the agent inside the bundle, whose version line names the version of every bundle.</summary>
    private const string AgentFile = "hostpanel-ssh-key-agent";

    /// <summary>Where the agent installer looks for the profile of a provider, below the bundle root.</summary>
    private const string ProfileFolder = "profiles/";

    /// <summary>The extension of a profile file.</summary>
    private const string ProfileExtension = ".conf";

    /// <summary>Where the agent installer looks for the helpers of a provider, below the bundle root.</summary>
    private const string HelperFolder = "providers/";

    /// <summary>The plugins whose profiles can be bundled.</summary>
    private readonly IPluginRegistry _plugins;

    /// <summary>The agent version, read from the embedded agent.</summary>
    private readonly string _version;

    /// <summary>The files every bundle holds.</summary>
    private readonly IReadOnlyList<SshAgentFile> _builtInFiles;

    /// <summary>The bundles already packed for a plugin's profile, by profile id.</summary>
    private readonly ConcurrentDictionary<string, ISshAgentBundle> _packed = new(StringComparer.Ordinal);

    /// <summary>Initialises the bundles from the embedded files.</summary>
    /// <param name="plugins">The plugins whose profiles can be bundled.</param>
    public SshAgentBundles(IPluginRegistry plugins)
    {
        _plugins = plugins;
        var assembly = typeof(SshAgentBundles).Assembly;
        _builtInFiles = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Select(name => new SshAgentFile(name[ResourcePrefix.Length..], Read(assembly, name)))
            .ToList();

        var agent = _builtInFiles.Single(f => f.Path == AgentFile);
        _version = VersionPattern().Match(System.Text.Encoding.UTF8.GetString(agent.Bytes)).Groups[1].Value;
        BuiltIn = new SshAgentBundle(_version, _builtInFiles);
    }

    /// <inheritdoc/>
    public ISshAgentBundle BuiltIn { get; }

    /// <inheritdoc/>
    public ISshAgentBundle? For(string profile)
    {
        if (string.Equals(profile, PluginSshAgentLayout.ReservedProfile, StringComparison.Ordinal))
        {
            return BuiltIn;
        }

        var shipped = _plugins.FindAgentProfile(profile);
        return shipped is null ? null : _packed.GetOrAdd(profile, _ => Pack(shipped));
    }

    /// <summary>Packs the built in files with a plugin's profile and helpers on top.</summary>
    /// <param name="shipped">The profile the plugin ships, checked at load.</param>
    /// <returns>The bundle that installs the plugin's profile.</returns>
    private SshAgentBundle Pack(PluginAgentProfileDto shipped)
    {
        var files = new List<SshAgentFile>(_builtInFiles)
        {
            new($"{ProfileFolder}{shipped.ProfileId}{ProfileExtension}", shipped.Profile),
        };
        files.AddRange(shipped.Helpers.Select(helper => new SshAgentFile($"{HelperFolder}{shipped.ProfileId}/{helper.Key}", helper.Value)));
        return new SshAgentBundle(_version, files);
    }

    /// <summary>Reads one embedded file.</summary>
    /// <param name="assembly">The assembly.</param>
    /// <param name="name">Resource name.</param>
    /// <returns>Its bytes.</returns>
    private static byte[] Read(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    /// <summary>The agent's version line.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex("^AGENT_VERSION = \"([^\"]+)\"$", RegexOptions.Multiline)]
    private static partial Regex VersionPattern();
}
