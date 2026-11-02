using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Innovayse.SDK.Plugins.Endpoints;
using Innovayse.SDK.Plugins.Enums;

namespace Innovayse.Hostpanel.Application.Plugins.Commands.CallAdminPlugin;

/// <summary>
/// Handles <see cref="CallAdminPluginCommand"/>. The admin area takes no route permission: the admin policy on the
/// controller is the whole check, and the plugin sees the administrator only as an opaque user reference, with no
/// client and no client permissions.
/// </summary>
public sealed class CallAdminPluginHandler
{
    /// <summary>Matches the request to a route and calls the plugin.</summary>
    private readonly PluginCallRunner _runner;

    /// <summary>The signed-in administrator; its subject becomes the plugin's opaque user reference.</summary>
    private readonly ICurrentUserService _currentUser;

    /// <summary>Makes the opaque references the plugin sees.</summary>
    private readonly IPluginReferences _references;

    /// <summary>Initializes the handler.</summary>
    /// <param name="runner">Matches the request to a route and calls the plugin.</param>
    /// <param name="currentUser">The signed-in administrator.</param>
    /// <param name="references">Makes the opaque references the plugin sees.</param>
    public CallAdminPluginHandler(PluginCallRunner runner, ICurrentUserService currentUser, IPluginReferences references)
    {
        _runner = runner;
        _currentUser = currentUser;
        _references = references;
    }

    /// <summary>Forwards the request to the plugin.</summary>
    /// <param name="command">The plugin id and the request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The plugin's reply; <c>PLUGIN_ROUTE_NOT_FOUND</c> when the route is not served; and what
    /// <see cref="PluginCallRunner.RunAsync"/> answers after that.
    /// </returns>
    public async Task<Result<PluginReplyDto>> HandleAsync(CallAdminPluginCommand command, CancellationToken ct)
    {
        var target = await _runner.FindAsync(command.PluginId, PluginArea.Admin, command.Input, ct);
        if (target is null)
        {
            return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.PLUGIN_ROUTE_NOT_FOUND);
        }

        var caller = new PluginCaller
        {
            ClientRef = null,
            UserRef = _references.ForUser(target.Manifest.Id, _currentUser.RequireUserId()),
        };

        return await _runner.RunAsync(target, caller, null, command.Input, ct);
    }
}
