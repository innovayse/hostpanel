using System.Collections.Concurrent;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// In-memory stand-in for every server's website and database agent. One <see cref="FakeHostingAgent"/> per server id,
/// created on first use, so tests that seed their own servers never see each other's sites.
/// </summary>
public sealed class FakeHostingResourceProviderFactory : IHostingResourceProviderFactory
{
    /// <summary>Agents by server id.</summary>
    private readonly ConcurrentDictionary<int, FakeHostingAgent> _agents = new();

    /// <summary>The agent of one server.</summary>
    /// <param name="serverId">The server.</param>
    /// <returns>Its fake agent.</returns>
    public FakeHostingAgent AgentFor(int serverId)
    {
        return _agents.GetOrAdd(serverId, id => new FakeHostingAgent(id));
    }

    /// <inheritdoc/>
    public IHostingResourceProvider? CreateFor(ServerSshTargetDto target)
    {
        return target.Module is ServerModules.Cwp7 ? AgentFor(target.ServerId) : null;
    }
}
