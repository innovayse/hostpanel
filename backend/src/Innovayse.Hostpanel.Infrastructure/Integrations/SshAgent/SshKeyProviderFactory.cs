using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent.Extensions;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent;

/// <summary>
/// Default <see cref="ISshKeyProviderFactory"/>. A server is served by the agent-backed provider when the plugin that serves its
/// module names an <c>sshAgentProfile</c> in its manifest. The one transitional exception is the profile the host still ships
/// for a legacy module id (<see cref="ServerModules.Cwp7"/>): it applies only while no plugin supplies a profile for the module,
/// and goes when that profile does. Every other module answers unsupported.
/// </summary>
/// <remarks>
/// Public, like every adapter the container registers: the SSH settings and setup queries take it, and
/// Wolverine builds their handlers with plain constructor calls, refusing service location. The
/// agent-backed provider it returns stays internal; callers see only <see cref="ISshKeyProvider"/>.
/// </remarks>
public sealed class SshKeyProviderFactory : ISshKeyProviderFactory
{
    /// <summary>
    /// Transitional: the profile the host still embeds for a legacy module id, used only when no plugin supplies a profile for the
    /// module. Removed together with the embedded profile once its plugin ships it.
    /// </summary>
    private const string TransitionalLegacyProfile = "cwp";

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
        if (match.Outcome == ServerModulePluginOutcome.Served)
        {
            var shipped = _plugins.GetLoadedManifests().FirstOrDefault(manifest => manifest.Id == match.PluginId)?.SshAgentProfile;
            if (shipped is not null)
            {
                return shipped;
            }
        }

        return ServerModules.Same(module, ServerModules.Cwp7) ? TransitionalLegacyProfile : null;
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Thrown when the settings are missing or incomplete.</exception>
    public ISshKeyProvider? CreateFor(ServerSshTargetDto target)
    {
        return Supports(target.Module) ? new AgentSshKeyProvider(target.ToAgentEndpoint(), _transport.SendAsync) : null;
    }
}
