using System.Text.Json.Nodes;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent;

/// <summary>
/// <see cref="IHostingResourceProvider"/> backed by <c>hostpanel-ssh-key-agent</c> (contract v1, 1.5.0+): websites and
/// databases through the agent's <c>ensure-website</c>, <c>remove-website</c>, <c>ensure-database</c> and
/// <c>remove-database</c> operations. One instance is bound to one server and refuses an account of another one. The
/// request and answer shapes are pinned by the agent's golden vectors in <c>backend/agent/ssh-key-agent/tests/contract</c>.
/// </summary>
internal sealed class AgentHostingResourceProvider : IHostingResourceProvider
{
    /// <summary>The agent operation that sets a website up.</summary>
    private const string EnsureWebsiteOp = "ensure-website";

    /// <summary>The agent operation that takes a website down.</summary>
    private const string RemoveWebsiteOp = "remove-website";

    /// <summary>The agent operation that creates a database.</summary>
    private const string EnsureDatabaseOp = "ensure-database";

    /// <summary>The agent operation that drops a database.</summary>
    private const string RemoveDatabaseOp = "remove-database";

    /// <summary>
    /// The shortest operation timeout a hosting operation gets: the agent lets a helper run for 50 seconds (a web server
    /// reload, a database creation), so the server's own, shorter timeout would cut a working call off.
    /// </summary>
    private static readonly TimeSpan HostingOperationTimeout = TimeSpan.FromSeconds(70);

    /// <summary>This server's conversation with its agent: requests, contract version and refusals.</summary>
    private readonly AgentConversation _agent;

    /// <summary>Initialises a provider bound to one server.</summary>
    /// <param name="endpoint">This server's agent endpoint.</param>
    /// <param name="send">Carries each request to the agent over SSH.</param>
    public AgentHostingResourceProvider(SshAgentEndpoint endpoint, SshAgentSend send)
    {
        _agent = new AgentConversation(endpoint, send);
    }

    /// <inheritdoc/>
    public async Task<HostingCapabilities> GetCapabilitiesAsync(CancellationToken ct)
    {
        var response = await _agent.SendAsync(new JsonObject { ["op"] = "ping" }, ct);
        var operations = TextsOf(response["capabilities"]);
        return new HostingCapabilities(
            operations.Contains(EnsureWebsiteOp) && operations.Contains(RemoveWebsiteOp),
            operations.Contains(EnsureDatabaseOp) && operations.Contains(RemoveDatabaseOp),
            TextsOf(response["php_versions"]));
    }

    /// <inheritdoc/>
    public async Task<ProvisionedWebsite> EnsureWebsiteAsync(WebsiteOrder order, CancellationToken ct)
    {
        var request = _agent.Request(EnsureWebsiteOp, order.Account);
        request["hostname"] = order.Hostname;
        request["docroot"] = order.DocumentRoot;
        if (order.PhpVersion is not null)
        {
            request["php"] = order.PhpVersion;
        }

        if (order.DocumentRootMayBeLink)
        {
            request["link_root"] = true;
        }

        var response = await _agent.SendAsync(request, LongEnough(), ct);

        // An agent older than 1.6.0, or a server whose helper does not name the binary, sends no php_cli.
        var phpCli = response["php_cli"]?.GetValue<string>();
        return new ProvisionedWebsite(string.IsNullOrWhiteSpace(phpCli) ? null : phpCli);
    }

    /// <inheritdoc/>
    public async Task RemoveWebsiteAsync(WebsiteRemoval removal, CancellationToken ct)
    {
        var request = _agent.Request(RemoveWebsiteOp, removal.Account);
        request["hostname"] = removal.Hostname;
        await _agent.SendAsync(request, LongEnough(), ct);
    }

    /// <inheritdoc/>
    public async Task<ProvisionedDatabase> EnsureDatabaseAsync(DatabaseOrder order, CancellationToken ct)
    {
        var request = _agent.Request(EnsureDatabaseOp, order.Account);
        request["label"] = order.Label;
        var response = await _agent.SendAsync(request, LongEnough(), ct);

        var name = response["database"]?.GetValue<string>();
        var username = response["username"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(username))
        {
            throw new SshProviderException(
                SshProviderFailure.InternalError, $"Agent on server {_agent.Endpoint.ServerId} answered a database without names.");
        }

        // The agent sends the password only for a database it just created, and the answer is the one place it exists.
        var password = response["created"]?.GetValue<bool>() == true ? response["password"]?.GetValue<string>() : null;
        return new ProvisionedDatabase(name, username, password);
    }

    /// <inheritdoc/>
    public async Task RemoveDatabaseAsync(DatabaseRemoval removal, CancellationToken ct)
    {
        var request = _agent.Request(RemoveDatabaseOp, removal.Account);
        request["label"] = removal.Label;
        await _agent.SendAsync(request, LongEnough(), ct);
    }

    /// <summary>The server's endpoint, with an operation timeout that outlasts the agent's own helper limit.</summary>
    /// <returns>The endpoint to send a hosting operation over.</returns>
    private SshAgentEndpoint LongEnough()
    {
        return _agent.Endpoint.OperationTimeout >= HostingOperationTimeout
            ? _agent.Endpoint
            : _agent.Endpoint with { OperationTimeout = HostingOperationTimeout };
    }

    /// <summary>The text elements of a JSON array; an absent or non-array node, which an older agent sends, is empty.</summary>
    /// <param name="node">The node of the response.</param>
    /// <returns>The texts, in the agent's order.</returns>
    private static List<string> TextsOf(JsonNode? node)
    {
        return node is JsonArray array
            ? array.Select(item => item?.GetValue<string>() ?? string.Empty).Where(text => text.Length > 0).ToList()
            : [];
    }
}
