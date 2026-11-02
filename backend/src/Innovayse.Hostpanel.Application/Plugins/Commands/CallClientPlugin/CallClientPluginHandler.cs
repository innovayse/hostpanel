using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Innovayse.SDK.Plugins.Endpoints;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Manifest;

namespace Innovayse.Hostpanel.Application.Plugins.Commands.CallClientPlugin;

/// <summary>
/// Handles <see cref="CallClientPluginCommand"/>. The order is the contract: the route and the plugin's switch first, so
/// an undeclared route, a disabled plugin and an unknown one answer alike; then the caller's account and the route's
/// permission from <see cref="ICallerClientResolver"/>, the one source of truth for owners, linked users and identity
/// plugin members; and only then the plugin.
/// </summary>
public sealed class CallClientPluginHandler
{
    /// <summary>Matches the request to a route and calls the plugin.</summary>
    private readonly PluginCallRunner _runner;

    /// <summary>Resolves the account the caller acts for and the permissions held there.</summary>
    private readonly ICallerClientResolver _callerClient;

    /// <summary>The signed-in caller; its subject becomes the plugin's opaque user reference.</summary>
    private readonly ICurrentUserService _currentUser;

    /// <summary>Makes the opaque references the plugin sees.</summary>
    private readonly IPluginReferences _references;

    /// <summary>Initializes the handler.</summary>
    /// <param name="runner">Matches the request to a route and calls the plugin.</param>
    /// <param name="callerClient">Resolves the account the caller acts for and the permissions held there.</param>
    /// <param name="currentUser">The signed-in caller.</param>
    /// <param name="references">Makes the opaque references the plugin sees.</param>
    public CallClientPluginHandler(
        PluginCallRunner runner, ICallerClientResolver callerClient, ICurrentUserService currentUser, IPluginReferences references)
    {
        _runner = runner;
        _callerClient = callerClient;
        _currentUser = currentUser;
        _references = references;
    }

    /// <summary>Forwards the request to the plugin when the caller may make it.</summary>
    /// <param name="command">The plugin id and the request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The plugin's reply; <c>PLUGIN_ROUTE_NOT_FOUND</c> when the route is not served; <c>IDENTITY_PLUGIN_UNAVAILABLE</c>
    /// when the identity plugin could not say who the caller is; <c>CLIENT_PERMISSION_DENIED</c> when the caller does not
    /// hold the route's permission; <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller acts for no account; and what
    /// <see cref="PluginCallRunner.RunAsync"/> answers after that.
    /// </returns>
    public async Task<Result<PluginReplyDto>> HandleAsync(CallClientPluginCommand command, CancellationToken ct)
    {
        var target = await _runner.FindAsync(command.PluginId, PluginArea.Client, command.Input, ct);
        if (target is null)
        {
            return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.PLUGIN_ROUTE_NOT_FOUND);
        }

        var access = await _callerClient.ResolveAsync(ct);

        // The plugin that decides the account did not answer: a retryable 503, not a permission refusal the caller
        // could take for permanent.
        if (access.IdentityUnavailable)
        {
            return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.IDENTITY_PLUGIN_UNAVAILABLE);
        }

        var needed = RequiredPermission(target.Match.Route);
        if ((access.Permissions & needed) != needed)
        {
            return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
        }

        // A caller with no single account (none, or linked to several) has nothing to act for: a client area call
        // always has a client, which is what the SDK promises the plugin.
        if (access.ClientId is not { } clientId)
        {
            return Result<PluginReplyDto>.FailureCode(HostpanelErrorCodes.CLIENT_PROFILE_NOT_FOUND);
        }

        var pluginId = target.Manifest.Id;
        var caller = new PluginCaller
        {
            ClientRef = _references.ForClient(pluginId, clientId),
            UserRef = _references.ForUser(pluginId, _currentUser.RequireUserId()),
            Permissions = HeldNames(access.Permissions),
        };

        return await _runner.RunAsync(target, caller, clientId, command.Input, ct);
    }

    /// <summary>The permission a route needs.</summary>
    /// <param name="route">The declared route.</param>
    /// <returns>
    /// <see cref="ClientPermission.None"/> for <see cref="PluginPermissions.Any"/>, the named flag for a defined permission,
    /// and <see cref="ClientPermission.All"/> for a route that declares none or a name nobody defined: like every portal
    /// action, a route that does not say who may call it is for the account holder alone, never for everyone.
    /// </returns>
    private static ClientPermission RequiredPermission(PluginRoute route)
    {
        if (route.Permission == PluginPermissions.Any)
        {
            return ClientPermission.None;
        }

        // IsDefined on a string matches defined names only, so "7" or "ViewDomains, ViewEmails" never parse.
        return route.Permission is not null
            && Enum.IsDefined(typeof(ClientPermission), route.Permission)
            && Enum.TryParse<ClientPermission>(route.Permission, out var permission)
            && permission != ClientPermission.None
                ? permission
                : ClientPermission.All;
    }

    /// <summary>The names of the single flags a caller holds, as the plugin lists them.</summary>
    /// <param name="held">The permissions the caller holds on the account.</param>
    /// <returns>The flag names; neither <c>None</c> nor the combined <c>All</c>.</returns>
    private static List<string> HeldNames(ClientPermission held)
    {
        return Enum.GetValues<ClientPermission>()
            .Where(flag => flag is not (ClientPermission.None or ClientPermission.All) && held.HasFlag(flag))
            .Select(flag => flag.ToString())
            .ToList();
    }
}
