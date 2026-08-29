namespace Innovayse.API.Clients;

using Innovayse.API.Clients.Requests;
using Innovayse.Application.Clients.Commands.InviteUserToClient;
using Innovayse.Application.Clients.Commands.RemoveUserFromClient;
using Innovayse.Application.Clients.Common;
using Innovayse.Application.Clients.Queries.GetClientUsers;
using Innovayse.Application.Clients.Queries.GetMyProfile;
using Innovayse.Domain.Auth;
using Innovayse.Domain.Clients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Client portal endpoints for the people linked to the caller's own account.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
/// <remarks>
/// The equivalent admin routes live on <see cref="ClientsController"/> and take the client id
/// in the path. Here it is resolved from the credential instead, so a client can only ever list,
/// invite and remove people on their own account.
/// </remarks>
[ApiController]
[Route("api/me/users")]
[Authorize(Roles = Roles.Client)]
public sealed class MyUsersController(IMessageBus bus) : ControllerBase
{
    /// <summary>
    /// The permission keys the client portal sends, in the order it lists them.
    /// </summary>
    /// <remarks>
    /// The portal sends keys rather than the bit-flags integer the admin endpoint takes, and
    /// this is the only place that knows both spellings. That is deliberate: the numbers are an
    /// internal encoding, and a browser that can post an arbitrary integer can grant a permission
    /// the UI never offered. An unrecognised key is dropped rather than refused — see
    /// <see cref="ParsePermissions"/>.
    /// </remarks>
    private static readonly Dictionary<string, ClientPermission> PermissionKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["profile"]        = ClientPermission.ModifyMasterProfile,
        ["contacts"]       = ClientPermission.ViewManageContacts,
        ["products"]       = ClientPermission.ViewProductsServices,
        ["passwords"]      = ClientPermission.ViewModifyPasswords,
        ["sso"]            = ClientPermission.AllowSingleSignOn,
        ["domains"]        = ClientPermission.ViewDomains,
        ["domainsettings"] = ClientPermission.ManageDomainSettings,
        ["invoices"]       = ClientPermission.ViewPayInvoices,
        ["quotes"]         = ClientPermission.ViewAcceptQuotes,
        ["tickets"]        = ClientPermission.ViewOpenSupportTickets,
        ["affiliate"]      = ClientPermission.ViewManageAffiliate,
        ["emails"]         = ClientPermission.ViewEmails,
        ["orders"]         = ClientPermission.PlaceNewOrders,
    };

    /// <summary>
    /// Lists the people linked to the authenticated client's own account.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The account's users with their permissions.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClientUserDto>>> GetMyUsersAsync(CancellationToken ct)
    {
        var profile = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(), ct);
        var users = await bus.InvokeAsync<IReadOnlyList<ClientUserDto>>(
            new GetClientUsersQuery(profile.Id), ct);

        return Ok(users);
    }

    /// <summary>
    /// Invites someone to the authenticated client's own account.
    /// </summary>
    /// <param name="request">Who to invite and what they may do.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("invite")]
    public async Task<IActionResult> InviteMyUserAsync(
        [FromBody] InvitePortalUserRequest request, CancellationToken ct)
    {
        var profile = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(), ct);

        await bus.InvokeAsync(
            new InviteUserToClientCommand(
                profile.Id, request.Email, request.FirstName, request.LastName,
                ParsePermissions(request.Permissions)),
            ct);

        return NoContent();
    }

    /// <summary>
    /// Removes someone from the authenticated client's own account.
    /// </summary>
    /// <param name="userId">The Identity user id to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpDelete("{userId}")]
    public async Task<IActionResult> RemoveMyUserAsync(string userId, CancellationToken ct)
    {
        var profile = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(), ct);
        await bus.InvokeAsync(new RemoveUserFromClientCommand(profile.Id, userId), ct);
        return NoContent();
    }

    /// <summary>
    /// Turns the portal's permission keys into the stored bit-flags value.
    /// </summary>
    /// <param name="permissions">
    /// The literal <c>all</c>, a comma-separated list of keys from <see cref="PermissionKeys"/>,
    /// or null.
    /// </param>
    /// <returns>The permissions to grant; none when nothing recognisable was sent.</returns>
    /// <remarks>
    /// Unknown keys are ignored rather than rejected, and the reason is which way the mistake
    /// falls: dropping one grants less than was asked for, while refusing the whole request over
    /// a single stale key would block an invitation the sender can do nothing about. Nothing
    /// recognisable at all therefore grants nothing — never everything.
    /// </remarks>
    private static int ParsePermissions(string? permissions)
    {
        if (string.IsNullOrWhiteSpace(permissions)) return (int)ClientPermission.None;

        if (permissions.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
            return (int)ClientPermission.All;

        var granted = ClientPermission.None;
        foreach (var key in permissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (PermissionKeys.TryGetValue(key, out var permission))
                granted |= permission;
        }

        return (int)granted;
    }
}
