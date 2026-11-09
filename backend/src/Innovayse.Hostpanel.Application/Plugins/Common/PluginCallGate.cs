using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common.Enums;
using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// The one place that decides whether a plugin call is served or refused as "no such route". The use cases and the rate
/// limit filter both ask it, so the filter measures exactly the calls the use case would let through and its 429 can never
/// tell an installed plugin from an absent one.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order is the contract, and the cheap checks come first.</b> For a client call: the caller's account (the same
/// answer for every plugin), then the manifest and the route match (memory only, no I/O), then the route's permission, and
/// only then the plugin's switch and settings, which read the database. A refusal for an unknown plugin, an undeclared
/// route, a wrong method or a missing permission therefore never reads the plugin's settings, so how long it takes does not
/// tell an installed plugin from an absent one. An admin call has no account and no permission and skips those steps.
/// </para>
/// </remarks>
public sealed class PluginCallGate
{
    /// <summary>Finds the manifest and checks that the plugin is switched on.</summary>
    private readonly IPluginEndpointResolver _plugins;

    /// <summary>Resolves the account the client caller acts for and the permissions held there.</summary>
    private readonly ICallerClientResolver _callerClient;

    /// <summary>Initializes a new instance of the <see cref="PluginCallGate"/> class.</summary>
    /// <param name="plugins">Finds the manifest and checks that the plugin is switched on.</param>
    /// <param name="callerClient">Resolves the account the client caller acts for and the permissions held there.</param>
    public PluginCallGate(IPluginEndpointResolver plugins, ICallerClientResolver callerClient)
    {
        _plugins = plugins;
        _callerClient = callerClient;
    }

    /// <summary>Decides a client area call.</summary>
    /// <param name="pluginId">The plugin id from the URL.</param>
    /// <param name="input">The request, as the controller read it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The decision; the caller's account is in it whenever it was resolved.</returns>
    public async Task<PluginCallGateResult> EvaluateClientAsync(string pluginId, PluginHttpInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var access = await _callerClient.ResolveAsync(ct);

        // The plugin that decides the account did not answer: a retryable 503, settled before the route is looked at, like
        // the missing account below, so neither can tell an installed plugin from an absent one.
        if (access.IdentityUnavailable)
        {
            return new PluginCallGateResult(PluginCallGateOutcome.IdentityUnavailable);
        }

        // A client area call always has a client, which is what the SDK promises the plugin.
        if (access.ClientId is not { } clientId)
        {
            return new PluginCallGateResult(PluginCallGateOutcome.NoAccount);
        }

        return await EvaluateRouteAsync(pluginId, PluginArea.Client, manifest => manifest.Api.Client, input, access, clientId, ct);
    }

    /// <summary>Decides an admin area call.</summary>
    /// <param name="pluginId">The plugin id from the URL.</param>
    /// <param name="input">The request, as the controller read it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The decision.</returns>
    public async Task<PluginCallGateResult> EvaluateAdminAsync(string pluginId, PluginHttpInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        return await EvaluateRouteAsync(pluginId, PluginArea.Admin, manifest => manifest.Api.Admin, input, null, null, ct);
    }

    /// <summary>
    /// Decides a public callback: a server to server call with no caller, such as a payment gateway's webhook. The same
    /// steps as the other doors without the ones that need a caller: the manifest and the route match (memory only), then
    /// the plugin's switch and settings. Only a route the manifest declares under <c>api.callbacks</c> is served; the
    /// client and admin routes of the same plugin are never reachable through here.
    /// </summary>
    /// <param name="pluginId">The plugin id from the URL.</param>
    /// <param name="input">The request, as the controller read it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The decision; every refusal is "no such route" to the caller.</returns>
    public async Task<PluginCallGateResult> EvaluateCallbackAsync(string pluginId, PluginHttpInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        return await EvaluateRouteAsync(
            pluginId, PluginCallbackContract.Area, PluginCallbackContract.RoutesOf, input, null, null, ct);
    }

    /// <summary>The route steps every door shares: match, permission (client only), then the plugin's switch.</summary>
    /// <param name="pluginId">The plugin id from the URL.</param>
    /// <param name="area">The panel the request came through.</param>
    /// <param name="routesOf">Picks, from the manifest, the routes the request is matched against.</param>
    /// <param name="input">The request.</param>
    /// <param name="access">The client caller's access; <see langword="null"/> for an admin call or a callback.</param>
    /// <param name="clientId">The account a client call acts for; <see langword="null"/> for an admin call or a callback.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The decision.</returns>
    private async Task<PluginCallGateResult> EvaluateRouteAsync(
        string pluginId,
        PluginArea area,
        Func<PluginManifest, IReadOnlyList<PluginRoute>> routesOf,
        PluginHttpInput input,
        CallerClientAccess? access,
        int? clientId,
        CancellationToken ct)
    {
        var held = access?.Permissions ?? ClientPermission.None;

        // Only the callback door reaches a non-app plugin that declares the endpoints capability; the client and admin
        // doors stay app only.
        var manifest = area == PluginCallbackContract.Area ? _plugins.FindForCallback(pluginId) : _plugins.Find(pluginId);
        if (manifest is null)
        {
            return new PluginCallGateResult(PluginCallGateOutcome.UnknownPlugin, ClientId: clientId, Permissions: held);
        }

        var routes = routesOf(manifest);
        var match = PluginRouteMatcher.Match(routes, input.Method, input.Path);
        if (match is null)
        {
            var outcome = DeclaredWithAnotherMethod(routes, input)
                ? PluginCallGateOutcome.WrongMethod
                : PluginCallGateOutcome.UndeclaredRoute;
            return new PluginCallGateResult(outcome, manifest, ClientId: clientId, Permissions: held);
        }

        if (access is not null && !PluginClientPermissions.IsHeld(access.Permissions, match.Route.Permission))
        {
            return new PluginCallGateResult(PluginCallGateOutcome.PermissionMissing, manifest, match.Route, clientId, null, held);
        }

        // The only step that reads the database, so it comes last: nothing above is slower for an installed plugin.
        var build = await _plugins.PrepareAsync(manifest.Id, ct);
        if (build is null)
        {
            return new PluginCallGateResult(PluginCallGateOutcome.Disabled, manifest, match.Route, clientId, null, held);
        }

        return new PluginCallGateResult(
            PluginCallGateOutcome.Admitted, manifest, match.Route, clientId, new PluginTarget(manifest, area, match, build), held);
    }

    /// <summary>Whether the path is declared in the area for a method other than the request's.</summary>
    /// <param name="routes">The routes of the area.</param>
    /// <param name="input">The request.</param>
    /// <returns><see langword="true"/> when some other method matches the same path.</returns>
    private static bool DeclaredWithAnotherMethod(IReadOnlyList<PluginRoute> routes, PluginHttpInput input)
    {
        return Enum.GetValues<PluginRouteMethod>()
            .Where(method => method != input.Method)
            .Any(method => PluginRouteMatcher.Match(routes, method, input.Path) is not null);
    }
}
