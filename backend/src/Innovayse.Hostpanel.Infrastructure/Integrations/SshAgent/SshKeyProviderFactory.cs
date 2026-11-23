using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent.Extensions;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent;

/// <summary>
/// Default <see cref="ISshKeyProviderFactory"/>. A server is served by the agent-backed provider when the plugin that serves its
/// module names an <c>sshAgentProfile</c> in its manifest; every other module answers unsupported. Nothing here names a module or a
/// profile: the plugin's manifest is the only source of both.
/// </summary>
/// <remarks>
/// Public, like every adapter the container registers: the SSH settings and setup queries take it, and
/// Wolverine builds their handlers with plain constructor calls, refusing service location. The
/// agent-backed provider it returns stays internal; callers see only <see cref="ISshKeyProvider"/>.
/// </remarks>
public sealed class SshKeyProviderFactory : ISshKeyProviderFactory
{
    /// <summary>The agent transport.</summary>
    private readonly SshAgentTransport _transport;

    /// <summary>Finds the plugin that serves a server's module.</summary>
    private readonly IServerModulePluginResolver _resolver;

    /// <summary>The loaded plugins, whose manifests name the profiles.</summary>
    private readonly IPluginRegistry _plugins;

    /// <summary>Initialises the factory.</summary>
    /// <param name="transport">The agent transport.</param>
    /// <param name="resolver">Finds the plugin that serves a server's module.</param>
    /// <param name="plugins">The loaded plugins, whose manifests name the profiles.</param>
    public SshKeyProviderFactory(SshAgentTransport transport, IServerModulePluginResolver resolver, IPluginRegistry plugins)
    {
        _transport = transport;
        _resolver = resolver;
        _plugins = plugins;
    }

    /// <inheritdoc/>
    public bool Supports(string module) => AgentProfileFor(module) is not null;

    /// <inheritdoc/>
    public string? AgentProfileFor(string module)
    {
        var match = _resolver.Find(module, PluginCapabilities.ProvisioningPanel);
        if (match.Outcome != ServerModulePluginOutcome.Served)
        {
            return null;
        }

        return _plugins.GetLoadedManifests().FirstOrDefault(manifest => manifest.Id == match.PluginId)?.SshAgentProfile;
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Thrown when the settings are missing or incomplete.</exception>
    public ISshKeyProvider? CreateFor(ServerSshTargetDto target)
    {
        return Supports(target.Module) ? new AgentSshKeyProvider(target.ToAgentEndpoint(), _transport.SendAsync) : null;
    }
}
