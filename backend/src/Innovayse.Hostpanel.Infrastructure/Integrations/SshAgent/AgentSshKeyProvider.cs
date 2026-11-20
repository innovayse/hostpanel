using System.Text.Json.Nodes;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Ssh;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent;

/// <summary>
/// <see cref="ISshKeyProvider"/> backed by <c>hostpanel-ssh-key-agent</c> (contract v1), the
/// helper installed on each managed server. One instance is bound to one server; it refuses any
/// account whose server id is not that server's, so a key operation can never be routed to the
/// wrong machine even if a caller builds the wrong <see cref="SshAccount"/>.
/// </summary>
internal sealed class AgentSshKeyProvider : ISshKeyProvider
{
    /// <summary>This server's conversation with its agent: requests, contract version and refusals.</summary>
    private readonly AgentConversation _agent;

    /// <summary>Initialises a provider bound to one server.</summary>
    /// <param name="endpoint">This server's agent endpoint.</param>
    /// <param name="send">Carries each request to the agent over SSH.</param>
    public AgentSshKeyProvider(SshAgentEndpoint endpoint, SshAgentSend send)
    {
        _agent = new AgentConversation(endpoint, send);
    }

    /// <summary>Gets the server this provider is bound to.</summary>
    public int ServerId => _agent.Endpoint.ServerId;

    /// <inheritdoc/>
    public async Task<SshAgentInfo> PingAsync(CancellationToken ct)
    {
        var response = await _agent.SendAsync(new JsonObject { ["op"] = "ping" }, ct);
        var provider = response["provider"]?.GetValue<string>();
        var binding = Guid.TryParse(response["binding"]?.GetValue<string>(), out var parsed) ? parsed : (Guid?)null;
        return new SshAgentInfo(response["agent_version"]?.GetValue<string>() ?? "unknown", provider, binding);
    }

    /// <inheritdoc/>
    public async Task<SshAccessStatus> GetAccessStatusAsync(SshAccount account, CancellationToken ct)
    {
        var response = await _agent.SendAsync(_agent.Request("status", account), ct);
        return new SshAccessStatus(
            response["shell_enabled"]?.GetValue<bool>() ?? false,
            response["managed_keys"]?.GetValue<int>() ?? 0,
            response["unmanaged_keys"]?.GetValue<int>() ?? 0);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<InstalledSshKey>> ListKeysAsync(SshAccount account, CancellationToken ct)
    {
        var response = await _agent.SendAsync(_agent.Request("list", account), ct);
        var keys = new List<InstalledSshKey>();
        foreach (var node in response["keys"]?.AsArray() ?? [])
        {
            if (Guid.TryParse(node?["key_id"]?.GetValue<string>(), out var keyId))
            {
                keys.Add(new InstalledSshKey(keyId, node?["fingerprint"]?.GetValue<string>() ?? string.Empty));
            }
        }

        return keys;
    }

    /// <inheritdoc/>
    public async Task AddKeyAsync(SshAccount account, Guid keyId, string publicKey, CancellationToken ct)
    {
        var request = _agent.Request("add", account);
        request["key_id"] = keyId.ToString("D");
        request["public_key"] = publicKey;
        await _agent.SendAsync(request, ct);
    }

    /// <inheritdoc/>
    public async Task RevokeKeyAsync(SshAccount account, Guid keyId, CancellationToken ct)
    {
        var request = _agent.Request("revoke", account);
        request["key_id"] = keyId.ToString("D");
        await _agent.SendAsync(request, ct);
    }

    /// <inheritdoc />
    public async Task<bool> RemoveDeletedAccountJailAsync(SshAccount account, CancellationToken ct)
    {
        var response = await _agent.SendAsync(_agent.Request("remove-jail", account), ct);
        return response["removed"]?.GetValue<bool>() == true;
    }
}
