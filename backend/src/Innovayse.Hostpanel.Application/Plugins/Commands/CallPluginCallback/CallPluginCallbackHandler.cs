using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Plugins.Commands.CallPluginCallback;

/// <summary>
/// Handles <see cref="CallPluginCallbackCommand"/>. <see cref="PluginCallGate"/> decides: an unknown plugin, a disabled or
/// unconfigured one, an undeclared route and a wrong method all answer the same 404 <c>PLUGIN_ROUTE_NOT_FOUND</c>, so a
/// refusal never reveals what is installed; only an admitted call reaches the plugin, with no client, no permissions and a
/// plugin-wide store.
/// </summary>
public sealed class CallPluginCallbackHandler
{
    /// <summary>The longest part of a caller-supplied plugin id the Debug log keeps.</summary>
    private const int LoggedIdMaxLength = 64;

    /// <summary>Decides whether the route is served.</summary>
    private readonly PluginCallGate _gate;

    /// <summary>Calls the plugin.</summary>
    private readonly PluginCallRunner _runner;

    /// <summary>Receives the real reason of a refusal, at Debug, for operators.</summary>
    private readonly ILogger<CallPluginCallbackHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="gate">Decides whether the route is served.</param>
    /// <param name="runner">Calls the plugin.</param>
    /// <param name="logger">Receives the real reason of a refusal, at Debug, for operators.</param>
    public CallPluginCallbackHandler(PluginCallGate gate, PluginCallRunner runner, ILogger<CallPluginCallbackHandler> logger)
    {
        _gate = gate;
        _runner = runner;
        _logger = logger;
    }

    /// <summary>Forwards the callback to the plugin when it declares the route.</summary>
    /// <param name="command">The plugin id and the request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The plugin's reply; <c>PLUGIN_ROUTE_NOT_FOUND</c>, one answer for every one of these reasons: no app plugin has that
    /// id, the plugin does not declare that path under <c>api.callbacks</c>, the path is declared for another method, or the
    /// plugin is switched off or not configured; and what <see cref="PluginCallRunner.RunAsync"/> answers after that. The
    /// real reason is only in the Debug log, and neither body nor headers are ever logged.
    /// </returns>
    public async Task<Result<PluginReplyDto>> HandleAsync(CallPluginCallbackCommand command, CancellationToken ct)
    {
        var decision = await _gate.EvaluateCallbackAsync(command.PluginId, command.Input, ct);
        if (decision.Target is not { } target)
        {
            _logger.LogDebug(
                "Plugin callback refused, reason {Reason}. Requested plugin {PluginId}.",
                decision.Outcome,
                Loggable(command.PluginId));
            return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.PLUGIN_ROUTE_NOT_FOUND);
        }

        // No client, no permissions, no user: the plugin sees no host facts and its plugin-wide store. The user reference is
        // a fixed marker, and no audit line is written (see PluginCallRunner.RunAsync).
        var binding = PluginCallBinding.WithoutClient(target.Manifest.Id);

        return await _runner.RunAsync(target, binding, PluginCallbackContract.CallerRef, command.Input, ct, recordActivity: false);
    }

    /// <summary>Makes a caller-supplied plugin id safe to write to a log line.</summary>
    /// <param name="pluginId">The id as the caller wrote it in the URL.</param>
    /// <returns>The id with carriage returns and line feeds removed, cut to <see cref="LoggedIdMaxLength"/> characters.</returns>
    private static string Loggable(string pluginId)
    {
        var clean = (pluginId ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
        return clean.Length <= LoggedIdMaxLength ? clean : clean[..LoggedIdMaxLength];
    }
}
