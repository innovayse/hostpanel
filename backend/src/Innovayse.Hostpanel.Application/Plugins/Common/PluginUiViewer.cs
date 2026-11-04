using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.SDK.Plugins.Enums;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// Who is looking at a panel's plugin pages, resolved the way the plugin route door resolves its caller, so a page is
/// shown to exactly the callers its routes answer.
/// </summary>
public static class PluginUiViewer
{
    /// <summary>The client permissions the caller of a panel holds for plugin pages.</summary>
    /// <param name="area">The panel.</param>
    /// <param name="callerClient">The one source of truth for the account the caller acts for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="ClientPermission.All"/> in the admin panel, which the admin policy alone guards and whose pages declare
    /// no permission; in the client panel the permissions held on the account the caller acts for,
    /// <c>IDENTITY_PLUGIN_UNAVAILABLE</c> when the identity plugin could not say who that is, or
    /// <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller acts for no single account.
    /// </returns>
    public static async Task<Result<ClientPermission>> PermissionsAsync(
        PluginArea area, ICallerClientResolver callerClient, CancellationToken ct)
    {
        if (area == PluginArea.Admin)
        {
            return Result<ClientPermission>.Success(ClientPermission.All);
        }

        var access = await callerClient.ResolveAsync(ct);

        // The plugin that decides the account did not answer: a retryable 503, not a refusal the caller could take for permanent.
        if (access.IdentityUnavailable)
        {
            return Result<ClientPermission>.FailureCode(HostpanelErrorCodes.IDENTITY_PLUGIN_UNAVAILABLE);
        }

        return access.ClientId is null
            ? Result<ClientPermission>.FailureCode(HostpanelErrorCodes.CLIENT_PROFILE_NOT_FOUND)
            : Result<ClientPermission>.Success(access.Permissions);
    }
}
