using Innovayse.Hostpanel.Application.Services.Commands.RevokePluginIssuedSshKeys;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Domain.Plugins.Events;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Events;

/// <summary>
/// When a plugin is removed, dispatches <see cref="RevokePluginIssuedSshKeysCommand"/>, which revokes every live SSH key
/// the host issued to it. The keys belong to this module, so the Services module takes them back; the plugin's stored data
/// is not touched here (that is the operator's separate purge request).
/// </summary>
/// <remarks>
/// Unlike the suspension handlers, this one throws when an agent did not confirm a revoke: handlers of one event are
/// separated (each its own durable queue), so the throw retries only this handler, through the durable retry policy
/// (cooldowns, then durable reschedules), and a key that might still log in is never given up on silently. The command is
/// idempotent, so a retry touches only the keys still live.
/// </remarks>
public sealed class RevokeSshKeysOnPluginRemovedHandler
{
    /// <summary>Dispatches the command.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Records each pass, so staff can see keys that are still waiting for their server.</summary>
    private readonly ILogger<RevokeSshKeysOnPluginRemovedHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">Dispatches the command.</param>
    /// <param name="logger">Records each pass, so staff can see keys that are still waiting for their server.</param>
    public RevokeSshKeysOnPluginRemovedHandler(IMessageBus bus, ILogger<RevokeSshKeysOnPluginRemovedHandler> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    /// <summary>Handles <see cref="PluginRemovedEvent"/>.</summary>
    /// <param name="evt">The removal.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once every key of the plugin is revoked.</returns>
    /// <exception cref="InvalidOperationException">
    /// The command was refused, or an agent did not confirm a revoke; the delivery is retried.
    /// </exception>
    public async Task HandleAsync(PluginRemovedEvent evt, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PluginSshKeyRevocationDto>>(
            new RevokePluginIssuedSshKeysCommand(evt.PluginId), ct);
        if (outcome.IsFailure || outcome.Value is not { } result)
        {
            // A refusal is not "nothing to revoke": the keys may still log in, so the delivery fails and is retried.
            _logger.LogWarning(
                "Revoking the SSH keys of removed plugin {PluginId} was refused with {Code}; the delivery is retried.",
                evt.PluginId, outcome.ErrorCode);
            throw new InvalidOperationException(
                $"Revoking the SSH keys of a removed plugin was refused ({outcome.ErrorCode}); the delivery is retried.");
        }

        _logger.LogInformation(
            "Revoked {Revoked} SSH keys of removed plugin {PluginId}; {Failed} not confirmed.",
            result.Revoked, evt.PluginId, result.Failed);

        if (result.Failed > 0)
        {
            throw new InvalidOperationException(
                $"{result.Failed} SSH keys of a removed plugin were not confirmed revoked; the delivery is retried.");
        }
    }
}
