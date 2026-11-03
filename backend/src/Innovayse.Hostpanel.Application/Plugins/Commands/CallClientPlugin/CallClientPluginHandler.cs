using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Application.Plugins.Common.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Plugins.Commands.CallClientPlugin;

/// <summary>
/// Handles <see cref="CallClientPluginCommand"/>. The order is the contract and <see cref="PluginCallGate"/> owns it: the
/// caller's account from <see cref="ICallerClientResolver"/>, the one source of truth for owners, linked users and identity
/// plugin members, first, so its answers are the same for every plugin; then the manifest and the route (memory only), the
/// route's permission, and only last the plugin's switch, the one step that reads settings. An unknown plugin, a disabled
/// one, an undeclared route, a wrong method and a route the caller may not use all answer the same 404
/// <c>PLUGIN_ROUTE_NOT_FOUND</c>, so a refusal never reveals what is installed; only then the plugin.
/// </summary>
public sealed class CallClientPluginHandler
{
    /// <summary>The longest part of a client-supplied plugin id the Debug log keeps.</summary>
    private const int LoggedIdMaxLength = 64;

    /// <summary>Decides whether the route is served and the caller may use it.</summary>
    private readonly PluginCallGate _gate;

    /// <summary>Calls the plugin.</summary>
    private readonly PluginCallRunner _runner;

    /// <summary>The signed-in caller; its subject becomes the plugin's opaque user reference.</summary>
    private readonly ICurrentUserService _currentUser;

    /// <summary>Makes the opaque references the plugin sees.</summary>
    private readonly IPluginReferences _references;

    /// <summary>Receives the real reason of a refusal, at Debug, for operators.</summary>
    private readonly ILogger<CallClientPluginHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="gate">Decides whether the route is served and the caller may use it.</param>
    /// <param name="runner">Calls the plugin.</param>
    /// <param name="currentUser">The signed-in caller.</param>
    /// <param name="references">Makes the opaque references the plugin sees.</param>
    /// <param name="logger">Receives the real reason of a refusal, at Debug, for operators.</param>
    public CallClientPluginHandler(
        PluginCallGate gate,
        PluginCallRunner runner,
        ICurrentUserService currentUser,
        IPluginReferences references,
        ILogger<CallClientPluginHandler> logger)
    {
        _gate = gate;
        _runner = runner;
        _currentUser = currentUser;
        _references = references;
        _logger = logger;
    }

    /// <summary>Forwards the request to the plugin when the caller may make it.</summary>
    /// <param name="command">The plugin id and the request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The plugin's reply; <c>IDENTITY_PLUGIN_UNAVAILABLE</c> when the identity plugin could not say who the caller is;
    /// <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller acts for no account (settled first, for every plugin);
    /// <c>PLUGIN_ROUTE_NOT_FOUND</c>, one answer for every one of these reasons: no app plugin has that id, the plugin does
    /// not declare that path in the client area, the path is declared for another method, the caller does not hold the
    /// route's permission, or the plugin is switched off or not configured; and what
    /// <see cref="PluginCallRunner.RunAsync"/> answers after that. The real reason is only in the Debug log.
    /// </returns>
    public async Task<Result<PluginReplyDto>> HandleAsync(CallClientPluginCommand command, CancellationToken ct)
    {
        var decision = await _gate.EvaluateClientAsync(command.PluginId, command.Input, ct);
        switch (decision.Outcome)
        {
            case PluginCallGateOutcome.Admitted:
                break;
            case PluginCallGateOutcome.IdentityUnavailable:
                return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.IDENTITY_PLUGIN_UNAVAILABLE);
            case PluginCallGateOutcome.NoAccount:
                return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.CLIENT_PROFILE_NOT_FOUND);
            case PluginCallGateOutcome.PermissionMissing:
                // The manifest id and the declared template are ours; the client's path and id are not logged here.
                _logger.LogDebug(
                    "Client plugin call refused, reason {Reason}. Plugin {PluginId}, route {Route}.",
                    decision.Outcome,
                    decision.Manifest?.Id,
                    decision.Route?.Path);
                return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.PLUGIN_ROUTE_NOT_FOUND);
            default:
                _logger.LogDebug(
                    "Client plugin call refused, reason {Reason}. Requested plugin {PluginId}.",
                    decision.Outcome,
                    Loggable(command.PluginId));
                return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.PLUGIN_ROUTE_NOT_FOUND);
        }

        // Admitted always carries its target and the account.
        var target = decision.Target!;
        var clientId = decision.ClientId!.Value;

        // The binding is built once, here: the reference the plugin sees on its caller is the scope of its store, and the
        // host facts follow the same permissions. The permissions are the caller's own, read again from the resolver, which
        // answers the same for the rest of the request.
        var pluginId = target.Manifest.Id;
        var binding = PluginCallBinding.ForClient(
            pluginId, clientId, _references.ForClient(pluginId, clientId), decision.Permissions);
        var userRef = _references.ForUser(pluginId, _currentUser.RequireUserId());

        return await _runner.RunAsync(target, binding, userRef, command.Input, ct);
    }

    /// <summary>Makes a client-supplied plugin id safe to write to a log line.</summary>
    /// <param name="pluginId">The id as the client wrote it in the URL.</param>
    /// <returns>The id with carriage returns and line feeds removed, cut to <see cref="LoggedIdMaxLength"/> characters.</returns>
    private static string Loggable(string pluginId)
    {
        var clean = (pluginId ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
        return clean.Length <= LoggedIdMaxLength ? clean : clean[..LoggedIdMaxLength];
    }
}
