using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent.Extensions;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent;

/// <summary>
/// Default <see cref="ISshKeyProviderFactory"/>. Servers of the v7 hosting panel module are served
/// by the agent-backed provider; every other module answers unsupported until it gets an
/// implementation of its own.
/// </summary>
/// <remarks>
/// Public, like every adapter the container registers: the SSH settings and setup queries take it, and
/// Wolverine builds their handlers with plain constructor calls, refusing service location. The
/// agent-backed provider it returns stays internal; callers see only <see cref="ISshKeyProvider"/>.
/// </remarks>
public sealed class SshKeyProviderFactory : ISshKeyProviderFactory
{
    /// <summary>
    /// The agent profile for the v7 hosting panel module: the name of <c>profiles/cwp.conf</c> in the
    /// agent bundle, which the installer takes as <c>--provider</c>.
    /// </summary>
    private const string HostingPanelV7Profile = "cwp";

    /// <summary>The agent transport.</summary>
    private readonly SshAgentTransport _transport;

    /// <summary>Initialises the factory.</summary>
    /// <param name="transport">The agent transport.</param>
    public SshKeyProviderFactory(SshAgentTransport transport)
    {
        _transport = transport;
    }

    /// <inheritdoc/>
    public bool Supports(ServerModule module) => AgentProfileFor(module) is not null;

    /// <inheritdoc/>
    public string? AgentProfileFor(ServerModule module) => module switch
    {
        ServerModule.Cwp7 => HostingPanelV7Profile,
        _ => null,
    };

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Thrown when the settings are missing or incomplete.</exception>
    public ISshKeyProvider? CreateFor(ServerSshTargetDto target)
    {
        return Supports(target.Module) ? new AgentSshKeyProvider(target.ToAgentEndpoint(), _transport.SendAsync) : null;
    }
}
