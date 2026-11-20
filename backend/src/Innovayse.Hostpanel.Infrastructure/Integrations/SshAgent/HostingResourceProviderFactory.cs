using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent.Extensions;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent;

/// <summary>
/// Default <see cref="IHostingResourceProviderFactory"/>. The agent that holds a server's keys also sets its sites up, so
/// a server is supported exactly when <see cref="ISshKeyProviderFactory"/> supports its module: the one mapping from a
/// server module to an agent profile stays in that factory and is not repeated here.
/// </summary>
public sealed class HostingResourceProviderFactory : IHostingResourceProviderFactory
{
    /// <summary>Tells which server modules have an agent profile.</summary>
    private readonly ISshKeyProviderFactory _agents;

    /// <summary>The agent transport.</summary>
    private readonly SshAgentTransport _transport;

    /// <summary>Initialises the factory.</summary>
    /// <param name="agents">Tells which server modules have an agent profile.</param>
    /// <param name="transport">The agent transport.</param>
    public HostingResourceProviderFactory(ISshKeyProviderFactory agents, SshAgentTransport transport)
    {
        _agents = agents;
        _transport = transport;
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Thrown when the settings are missing or incomplete.</exception>
    public IHostingResourceProvider? CreateFor(ServerSshTargetDto target)
    {
        return _agents.Supports(target.Module) ? new AgentHostingResourceProvider(target.ToAgentEndpoint(), _transport.SendAsync) : null;
    }
}
