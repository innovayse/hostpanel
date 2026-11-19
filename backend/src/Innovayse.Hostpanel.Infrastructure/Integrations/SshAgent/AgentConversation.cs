using System.Text.Json;
using System.Text.Json.Nodes;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent;

/// <summary>
/// One server's conversation with its agent (contract v1): builds a request for an account of that server, stamps the
/// contract version, sends it and checks what comes back. Every agent operation, key or hosting, speaks through it, so
/// the version check and the mapping of an agent refusal exist once.
/// </summary>
internal sealed class AgentConversation
{
    /// <summary>The agent contract version this conversation speaks.</summary>
    public const int ContractVersion = 1;

    /// <summary>Carries each request to the agent.</summary>
    private readonly SshAgentSend _send;

    /// <summary>Initialises a conversation bound to one server.</summary>
    /// <param name="endpoint">This server's agent endpoint.</param>
    /// <param name="send">Carries each request to the agent over SSH.</param>
    public AgentConversation(SshAgentEndpoint endpoint, SshAgentSend send)
    {
        Endpoint = endpoint;
        _send = send;
    }

    /// <summary>Gets this server's agent endpoint.</summary>
    public SshAgentEndpoint Endpoint { get; }

    /// <summary>Builds a request for an account, refusing an account of another server.</summary>
    /// <param name="op">Operation name.</param>
    /// <param name="account">The hosting account the request is for.</param>
    /// <returns>The request object.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the account belongs to another server.</exception>
    public JsonObject Request(string op, SshAccount account)
    {
        if (account.ServerId != Endpoint.ServerId)
        {
            throw new InvalidOperationException(
                $"SSH provider for server {Endpoint.ServerId} was asked to act on server {account.ServerId}.");
        }

        return new JsonObject { ["op"] = op, ["account"] = account.Username };
    }

    /// <summary>Sends a request over the server's endpoint and returns the parsed success response.</summary>
    /// <param name="request">The request (without version).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The response object.</returns>
    /// <exception cref="SshProviderException">Thrown on transport failure or an agent refusal.</exception>
    public Task<JsonObject> SendAsync(JsonObject request, CancellationToken ct)
    {
        return SendAsync(request, Endpoint, ct);
    }

    /// <summary>Sends a request over <paramref name="endpoint"/> (the server's, possibly with a longer timeout).</summary>
    /// <param name="request">The request (without version).</param>
    /// <param name="endpoint">The endpoint to use.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The response object.</returns>
    /// <exception cref="SshProviderException">Thrown on transport failure or an agent refusal.</exception>
    public async Task<JsonObject> SendAsync(JsonObject request, SshAgentEndpoint endpoint, CancellationToken ct)
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
            throw new SshProviderException(SshProviderFailure.InternalError, $"Agent on server {Endpoint.ServerId} answered non-JSON.");
        }

        if (response is null || response["v"]?.GetValue<int>() != ContractVersion)
        {
            throw new SshProviderException(SshProviderFailure.UnsupportedVersion, $"Agent on server {Endpoint.ServerId} speaks another contract version.");
        }

        if (response["ok"]?.GetValue<bool>() != true)
        {
            var code = response["error"]?.GetValue<string>();
            var failure = code is null ? SshProviderFailure.InternalError : AgentFailureCodes.Parse(code);
            throw new SshProviderException(failure, $"Agent on server {Endpoint.ServerId} refused: {code}.");
        }

        return response;
    }
}
