using System.Collections.Concurrent;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// In-memory stand-in for every server's SSH key agent. One <see cref="FakeSshAgent"/> per server
/// id, created on first use, so tests that seed their own servers never see each other's keys and
/// two servers with the same Linux username keep separate key sets.
/// </summary>
public sealed class FakeSshKeyProviderFactory : ISshKeyProviderFactory
{
    /// <summary>Agents by server id.</summary>
    private readonly ConcurrentDictionary<int, FakeSshAgent> _agents = new();

    /// <summary>The agent of one server.</summary>
    /// <param name="serverId">The server.</param>
    /// <returns>Its fake agent.</returns>
    public FakeSshAgent AgentFor(int serverId) => _agents.GetOrAdd(serverId, id => new FakeSshAgent(id));

    /// <inheritdoc/>
    public bool Supports(string module) => AgentProfileFor(module) is not null;

    /// <inheritdoc/>
    public string? AgentProfileFor(string module) => module is ServerModules.Linux ? "generic-linux" : null;

    /// <inheritdoc/>
    public ISshKeyProvider? CreateFor(ServerSshTargetDto target) =>
        Supports(target.Module) ? AgentFor(target.ServerId) : null;
}
