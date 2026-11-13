using System.Text.Json;
using System.Text.Json.Nodes;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent;

/// <summary>
/// <see cref="ISshKeyProvider"/> backed by <c>hostpanel-ssh-key-agent</c> (contract v1), the
/// helper installed on each managed server. One instance is bound to one server; it refuses any
/// account whose server id is not that server's, so a key operation can never be routed to the
/// wrong machine even if a caller builds the wrong <see cref="SshAccount"/>.
/// </summary>
internal sealed class AgentSshKeyProvider : ISshKeyProvider
{
    /// <summary>This server's agent endpoint.</summary>
    private readonly SshAgentEndpoint _endpoint;

    /// <summary>Carries each request to the agent.</summary>
    private readonly SshAgentSend _send;

    /// <summary>Initialises a provider bound to one server.</summary>
    /// <param name="endpoint">This server's agent endpoint.</param>
    /// <param name="send">Carries each request to the agent over SSH.</param>
    public AgentSshKeyProvider(SshAgentEndpoint endpoint, SshAgentSend send)
    {
        _endpoint = endpoint;
        _send = send;
    }

    /// <summary>The agent contract version this provider speaks.</summary>
    public const int ContractVersion = 1;

    /// <summary>Gets the server this provider is bound to.</summary>
    public int ServerId => _endpoint.ServerId;

    /// <inheritdoc/>
    public async Task<SshAgentInfo> PingAsync(CancellationToken ct)
    {
        var response = await SendAsync(new JsonObject { ["op"] = "ping" }, ct);
        var provider = response["provider"]?.GetValue<string>();
        var binding = Guid.TryParse(response["binding"]?.GetValue<string>(), out var parsed) ? parsed : (Guid?)null;
        return new SshAgentInfo(response["agent_version"]?.GetValue<string>() ?? "unknown", provider, binding);
    }

    /// <inheritdoc/>
    public async Task<SshAccessStatus> GetAccessStatusAsync(SshAccount account, CancellationToken ct)
    {
        var response = await SendAsync(Request("status", account), ct);
        return new SshAccessStatus(
            response["shell_enabled"]?.GetValue<bool>() ?? false,
            response["managed_keys"]?.GetValue<int>() ?? 0,
            response["unmanaged_keys"]?.GetValue<int>() ?? 0);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<InstalledSshKey>> ListKeysAsync(SshAccount account, CancellationToken ct)
    {
        var response = await SendAsync(Request("list", account), ct);
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
        var request = Request("add", account);
        request["key_id"] = keyId.ToString("D");
        request["public_key"] = publicKey;
        await SendAsync(request, ct);
    }

    /// <inheritdoc/>
    public async Task RevokeKeyAsync(SshAccount account, Guid keyId, CancellationToken ct)
    {
        var request = Request("revoke", account);
        request["key_id"] = keyId.ToString("D");
        await SendAsync(request, ct);
    }

    /// <summary>The longest a site preparation may run: the first one copies PHP into the jail.</summary>
    private static readonly TimeSpan PrepareSiteTimeout = TimeSpan.FromMinutes(10);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> PrepareSiteAsync(SshAccount account, string domain, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        var request = Request("prepare-site", account);
        request["domain"] = domain.Trim().ToLowerInvariant();
        var endpoint = _endpoint.OperationTimeout >= PrepareSiteTimeout ? _endpoint : _endpoint with { OperationTimeout = PrepareSiteTimeout };
        var response = await SendAsync(request, endpoint, ct);
        return response["steps"] is JsonArray steps
            ? steps.Select(s => s?.GetValue<string>() ?? string.Empty).Where(s => s.Length > 0).ToList()
            : [];
    }

    /// <summary>Builds a request for an account, refusing an account of another server.</summary>
    /// <param name="op">Operation name.</param>
    /// <param name="account">The hosting account the request is for.</param>
    /// <returns>The request object.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the account belongs to another server.</exception>
    private JsonObject Request(string op, SshAccount account)
    {
        if (account.ServerId != _endpoint.ServerId)
        {
            throw new InvalidOperationException(
                $"SSH provider for server {_endpoint.ServerId} was asked to act on server {account.ServerId}.");
        }

        return new JsonObject { ["op"] = op, ["account"] = account.Username };
    }

    /// <summary>Sends a request and returns the parsed success response.</summary>
    /// <param name="request">The request (without version).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The response object.</returns>
    /// <exception cref="SshProviderException">Thrown on transport failure or an agent refusal.</exception>
    private Task<JsonObject> SendAsync(JsonObject request, CancellationToken ct) => SendAsync(request, _endpoint, ct);

    /// <summary>Sends a request over <paramref name="endpoint"/> (the server's, possibly with a longer timeout).</summary>
    /// <param name="request">The request (without version).</param>
    /// <param name="endpoint">The endpoint to use.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The response object.</returns>
    /// <exception cref="SshProviderException">Thrown on transport failure or an agent refusal.</exception>
    private async Task<JsonObject> SendAsync(JsonObject request, SshAgentEndpoint endpoint, CancellationToken ct)
    {
        request["v"] = ContractVersion;
        var raw = await _send(endpoint, request.ToJsonString(), ct);

        JsonObject? response;
        try
        {
            response = JsonNode.Parse(raw) as JsonObject;
        }
        catch (JsonException)
        {
            throw new SshProviderException(SshProviderFailure.InternalError, $"Agent on server {_endpoint.ServerId} answered non-JSON.");
        }

        if (response is null || response["v"]?.GetValue<int>() != ContractVersion)
        {
            throw new SshProviderException(SshProviderFailure.UnsupportedVersion, $"Agent on server {_endpoint.ServerId} speaks another contract version.");
        }

        if (response["ok"]?.GetValue<bool>() != true)
        {
            var code = response["error"]?.GetValue<string>();
            var failure = code is null ? SshProviderFailure.InternalError : AgentFailureCodes.Parse(code);
            throw new SshProviderException(failure, $"Agent on server {_endpoint.ServerId} refused: {code}.");
        }

        return response;
    }
}
