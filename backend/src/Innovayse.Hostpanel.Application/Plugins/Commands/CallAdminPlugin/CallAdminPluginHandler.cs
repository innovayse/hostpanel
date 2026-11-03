using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;

namespace Innovayse.Hostpanel.Application.Plugins.Commands.CallAdminPlugin;

/// <summary>
/// Handles <see cref="CallAdminPluginCommand"/>. The admin area takes no route permission: the admin policy on the
/// controller is the whole check, and the plugin sees the administrator only as an opaque user reference, with no
/// client and no client permissions.
/// </summary>
public sealed class CallAdminPluginHandler
{
    /// <summary>Decides whether the route is served.</summary>
    private readonly PluginCallGate _gate;

    /// <summary>Calls the plugin.</summary>
    private readonly PluginCallRunner _runner;

    /// <summary>The signed-in administrator; its subject becomes the plugin's opaque user reference.</summary>
    private readonly ICurrentUserService _currentUser;

    /// <summary>Makes the opaque references the plugin sees.</summary>
    private readonly IPluginReferences _references;

    /// <summary>Initializes the handler.</summary>
    /// <param name="gate">Decides whether the route is served.</param>
    /// <param name="runner">Calls the plugin.</param>
    /// <param name="currentUser">The signed-in administrator.</param>
    /// <param name="references">Makes the opaque references the plugin sees.</param>
    public CallAdminPluginHandler(
        PluginCallGate gate, PluginCallRunner runner, ICurrentUserService currentUser, IPluginReferences references)
    {
        _gate = gate;
        _runner = runner;
        _currentUser = currentUser;
        _references = references;
    }

    /// <summary>Forwards the request to the plugin.</summary>
    /// <param name="command">The plugin id and the request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The plugin's reply; <c>PLUGIN_ROUTE_NOT_FOUND</c> when no app plugin has that id, the plugin does not declare that path
    /// or method in the admin area, or the plugin is switched off or not configured; and what
    /// <see cref="PluginCallRunner.RunAsync"/> answers after that.
    /// </returns>
    public async Task<Result<PluginReplyDto>> HandleAsync(CallAdminPluginCommand command, CancellationToken ct)
    {
        var decision = await _gate.EvaluateAdminAsync(command.PluginId, command.Input, ct);
        if (decision.Target is not { } target)
        {
            return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.PLUGIN_ROUTE_NOT_FOUND);
        }

        // No client, no reference and no permissions: the plugin sees no host facts and its plugin-wide store.
        var pluginId = target.Manifest.Id;
        var userRef = _references.ForUser(pluginId, _currentUser.RequireUserId());

        return await _runner.RunAsync(target, PluginCallBinding.WithoutClient(pluginId), userRef, command.Input, ct);
    }
}
