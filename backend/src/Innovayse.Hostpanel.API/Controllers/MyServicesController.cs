using Innovayse.Hostpanel.API.Authorization;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetMyServiceInvoices;
using Innovayse.Hostpanel.Application.Provisioning.Commands.ChangeMyServicePassword;
using Innovayse.Hostpanel.Application.Provisioning.Common;
using Innovayse.Hostpanel.Application.Provisioning.Queries.GetMyServiceControlPanelSsoUrl;
using Innovayse.Hostpanel.Application.Services.Commands.AddMySshKey;
using Innovayse.Hostpanel.Application.Services.Commands.CancelMyService;
using Innovayse.Hostpanel.Application.Services.Commands.OrderMyService;
using Innovayse.Hostpanel.Application.Services.Commands.RevokeMySshKey;
using Innovayse.Hostpanel.Application.Services.Commands.SetupMyService;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Application.Services.Queries.GetMyServiceCancellationStatus;
using Innovayse.Hostpanel.Application.Services.Queries.GetMyServiceHostingInfo;
using Innovayse.Hostpanel.Application.Services.Queries.GetMyServices;
using Innovayse.Hostpanel.Application.Services.Queries.GetMySshAccess;
using Innovayse.Hostpanel.Application.Services.Queries.ListMySshKeys;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Client portal endpoints for viewing and ordering services.
/// </summary>
/// <remarks>
/// <para>
/// No action here checks ownership, and none may: the rule lives in the client-facing
/// <c>My*</c> handlers, which resolve the caller from the credential themselves. That is not
/// only the thin-controller rule: two of the use cases behind these routes
/// (<c>GetControlPanelSsoUrlQuery</c>, <c>ChangePasswordCommand</c>) are also dispatched by the admin
/// <c>ProvisioningController</c>, where acting on any client's service is legitimate, so a check
/// written here would have guaranteed nothing about the message itself and a check written on
/// the shared use case would have broken the admin path.
/// </para>
/// <para>
/// Every action below that takes a service id off the route dispatches a message implementing
/// <see cref="Innovayse.Hostpanel.Application.Common.Interfaces.ICallerScopedServiceMessage"/>, and a
/// test requests every such route with another client's service id and expects 404. A service belonging to somebody
/// else, a service that does not exist, and a caller with no client record all answer 404 with
/// <c>SERVICE_NOT_FOUND</c> and the same sentence; service ids are sequential, and telling
/// those three apart is itself a way of enumerating them.
/// </para>
/// </remarks>
[Route("api/me/services")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class MyServicesController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public MyServicesController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns all services belonging to the authenticated client.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the caller's services; 401 without a session; 403 with <c>CLIENT_PERMISSION_DENIED</c>
    /// when the user lacks <c>ViewProductsServices</c>; 404 with <c>CLIENT_PROFILE_NOT_FOUND</c> when
    /// the caller has no client record;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewProductsServices)]
    [ProducesResponseType(typeof(IReadOnlyList<ClientServiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMineAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<ClientServiceDto>>>(new GetMyServicesQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>Orders a new service for the authenticated client.</summary>
    /// <param name="command">
    /// The product and billing cycle to order. Amounts are never taken from the request.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new service id; 400 with <c>VALIDATION_ERROR</c> for a malformed body or
    /// <c>PRODUCT_NOT_AVAILABLE</c> when the product or billing cycle cannot be ordered; 401 without a session; 403 with
    /// <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>PlaceNewOrders</c>, or with <c>CLIENT_SUSPENDED</c> or
    /// <c>CLIENT_CLOSED</c> when the account may not order; 404 with
    /// <c>CLIENT_PROFILE_NOT_FOUND</c> or <c>PRODUCT_NOT_FOUND</c>;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.PlaceNewOrders)]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> OrderAsync([FromBody] OrderMyServiceCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult("/api/me/services", id));
    }

    /// <summary>
    /// Generates a time-limited control-panel sign-in link for a service owned by the authenticated client.
    /// </summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the control-panel sign-in link; 400 with <c>VALIDATION_ERROR</c> for a non-positive id,
    /// <c>SERVICE_NOT_PROVISIONED</c>, <c>SERVICE_NO_SERVER_ASSIGNED</c> or, for a terminated service,
    /// <c>SERVICE_NOT_ACTIVE</c>; 401 without a session; 403 with
    /// <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>AllowSingleSignOn</c>, or <c>CLIENT_SSO_DISABLED</c> when the
    /// account does not allow single sign-on; 404 with
    /// <c>SERVICE_NOT_FOUND</c> when the service is not the caller's; 502 with
    /// <c>SERVICE_CPANEL_SSO_UPSTREAM_FAILED</c> when the provider cannot issue one;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}/cpanel-sso")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.AllowSingleSignOn)]
    [ProducesResponseType(typeof(ControlPanelSsoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetCPanelSsoUrlAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<ControlPanelSsoDto>>(
            new GetMyServiceControlPanelSsoUrlQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Submits a cancellation request for a client service: an immediate one terminates the service
    /// now, an end-of-period one marks it to end with its current cycle.
    /// </summary>
    /// <param name="id">Client service primary key; wins over any id in the body.</param>
    /// <param name="command">The cancellation type and an optional reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 on success; 400 with <c>VALIDATION_ERROR</c> for a malformed body; 401 without a session; 403 with
    /// <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>PlaceNewOrders</c>; 404 with
    /// <c>SERVICE_NOT_FOUND</c> when the service is not the caller's; 409 with
    /// <c>CANCELLATION_ALREADY_PENDING</c> when a request is already open or <c>SERVICE_ALREADY_TERMINATED</c>
    /// when the service has already ended;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.PlaceNewOrders)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelAsync(int id, [FromBody] CancelMyServiceCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { ServiceId = id }, ct);
        return HandleResultOk(outcome);
    }

    /// <summary>
    /// Sets up a pending service with hosting details (domain, username, password) and triggers
    /// provisioning.
    /// </summary>
    /// <param name="id">Client service primary key; wins over any id in the body.</param>
    /// <param name="command">The hosting details to set up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 on successful setup and provisioning; 400 with <c>VALIDATION_ERROR</c> or
    /// <c>SERVICE_NOT_PENDING</c>; 401 without a session; 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks
    /// <c>PlaceNewOrders</c>; 402 with <c>SERVICE_FIRST_INVOICE_UNPAID</c> when the first invoice is not paid in full;
    /// 404 with <c>SERVICE_NOT_FOUND</c> when the service is not the caller's;
    /// 409 with <c>PRODUCT_PACKAGE_MISSING</c> when the product names no hosting package,
    /// <c>PRODUCT_SERVER_GROUP_REQUIRED</c> when it has no server group and servers run several panels, or
    /// <c>PROVISIONING_ACCOUNT_CONFLICT</c> when the panel holds another account under the username;
    /// 502 with <c>PROVISIONING_UPSTREAM_FAILED</c> when the provider rejects the request; 503 with
    /// <c>NO_ELIGIBLE_SERVER</c> when no server has room;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/setup")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.PlaceNewOrders)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status402PaymentRequired)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SetupAsync(int id, [FromBody] SetupMyServiceCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { ServiceId = id }, ct);
        return HandleResultOk(outcome);
    }

    /// <summary>Returns the invoices charged to a service owned by the authenticated client.</summary>
    /// <remarks>
    /// The only action on this controller whose use case scopes itself to the caller. The reply
    /// carries the count of the caller's unattributable invoices alongside the list, because an
    /// empty list here means "nothing is recorded against this service", which is a weaker claim
    /// than "nothing was charged": see <see cref="ServiceInvoicesDto"/>.
    /// </remarks>
    /// <param name="id">Client service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the service's invoices; 400 with <c>VALIDATION_ERROR</c> for a non-positive id; 401 without a session;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewProductsServices</c> or
    /// <c>ViewPayInvoices</c>; 404 with <c>SERVICE_NOT_FOUND</c> when the service is not the caller's;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}/invoices")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewProductsServices | ClientPermission.ViewPayInvoices)]
    [ProducesResponseType(typeof(ServiceInvoicesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvoicesAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<ServiceInvoicesDto>>(new GetMyServiceInvoicesQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns the cancellation status for a client service.</summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the cancellation status; 400 with <c>VALIDATION_ERROR</c> for a non-positive id; 401 without a session;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewProductsServices</c>; 404
    /// with <c>SERVICE_NOT_FOUND</c> when the service is not the caller's;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}/cancellation-status")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewProductsServices)]
    [ProducesResponseType(typeof(CancellationStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCancellationStatusAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<CancellationStatusDto>>(
            new GetMyServiceCancellationStatusQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Reads the live nameservers and certificate of the domain on one of the caller's services.</summary>
    /// <remarks>
    /// The domain is read from the service, never from the request, and the probe refuses any name that
    /// is not a public host. Rate-limited as an upstream call: it reaches out over the network.
    /// </remarks>
    /// <param name="id">Client service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the nameservers and certificate (empty when the service has no domain or nothing could be
    /// read); 400 with <c>VALIDATION_ERROR</c> for a non-positive id; 401 without a session; 403 with
    /// <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewProductsServices</c>; 404 with
    /// <c>SERVICE_NOT_FOUND</c> when the service is not the caller's; 429 when the upstream rate limit
    /// is exceeded.
    /// </returns>
    [HttpGet("{id:int}/hosting-info")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewProductsServices)]
    [EnableRateLimiting(RateLimitPolicies.Upstream)]
    [ProducesResponseType(typeof(HostingInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetHostingInfoAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<HostingInfoDto>>(new GetMyServiceHostingInfoQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Changes the hosting account password for a client service.</summary>
    /// <param name="id">Client service primary key; wins over any id in the body.</param>
    /// <param name="command">The new password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 on success; 400 with <c>VALIDATION_ERROR</c>, <c>SERVICE_NOT_PROVISIONED</c> or
    /// <c>SERVICE_NO_SERVER_ASSIGNED</c>; 401 without a session; 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user
    /// lacks <c>ViewModifyPasswords</c>; 404 with <c>SERVICE_NOT_FOUND</c> when the service is not the
    /// caller's; 502 with <c>SERVICE_PASSWORD_UPSTREAM_FAILED</c> when the provider rejects the change;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/change-password")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewModifyPasswords)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> ChangePasswordAsync(
        int id, [FromBody] ChangeMyServicePasswordCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { ServiceId = id }, ct);
        return HandleResultOk(outcome);
    }

    /// <summary>Gets the SSH access summary of one of the caller's services.</summary>
    /// <remarks>
    /// Server, provider, hosting account, hostname and port are all resolved from the service on
    /// the server side. Rate-limited as an upstream call: it may ask the hosting server.
    /// </remarks>
    /// <param name="id">Client service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the summary; 400 with <c>VALIDATION_ERROR</c> for a non-positive id; 401 without a session; 403 with
    /// <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewProductsServices</c>; 404 with
    /// <c>SERVICE_NOT_FOUND</c> when the service is not the caller's; 429 when the upstream rate limit
    /// is exceeded.
    /// </returns>
    [HttpGet("{id:int}/ssh")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewProductsServices)]
    [EnableRateLimiting(RateLimitPolicies.Upstream)]
    [ProducesResponseType(typeof(SshAccessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetSshAccessAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<SshAccessDto>>(new GetMySshAccessQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Lists the SSH keys of one of the caller's services.</summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the keys; 400 with <c>VALIDATION_ERROR</c> for a non-positive id; 401 without a session; 403 with
    /// <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewProductsServices</c>; 404 with
    /// <c>SERVICE_NOT_FOUND</c> when the service is not the caller's; 409 with
    /// <c>SSH_ACCESS_UNAVAILABLE</c> when the service is inactive; 429 when the upstream rate limit is
    /// exceeded.
    /// </returns>
    [HttpGet("{id:int}/ssh/keys")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewProductsServices)]
    [EnableRateLimiting(RateLimitPolicies.Upstream)]
    [ProducesResponseType(typeof(IReadOnlyList<SshKeyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ListSshKeysAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<SshKeyDto>>>(new ListMySshKeysQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Adds an ED25519 public key to one of the caller's services.</summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="command">Label and public key. No server, account or path is accepted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the key; 400 with <c>VALIDATION_ERROR</c>, <c>SSH_KEY_INVALID</c> or
    /// <c>SSH_PRIVATE_KEY_REJECTED</c>; 401 without a session; 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user
    /// lacks <c>ViewModifyPasswords</c>; 404 with <c>SERVICE_NOT_FOUND</c>; 409 with
    /// <c>SSH_KEY_ALREADY_INSTALLED</c>, <c>SSH_KEY_LIMIT_REACHED</c> or <c>SSH_ACCESS_UNAVAILABLE</c>;
    /// 429 when the key rate limit is exceeded; 503 with <c>SSH_SERVER_UNAVAILABLE</c>.
    /// </returns>
    [HttpPost("{id:int}/ssh/keys")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewModifyPasswords)]
    [EnableRateLimiting(RateLimitPolicies.SshKeys)]
    [ProducesResponseType(typeof(SshKeyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> AddSshKeyAsync(
        int id, [FromBody] AddMySshKeyCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<SshKeyDto>>(command with { ServiceId = id }, ct);
        return HandleResult(outcome, key => CreatedResult($"/api/me/services/{id}/ssh/keys", key));
    }

    /// <summary>Revokes one SSH key of one of the caller's services.</summary>
    /// <param name="id">The client service id, like every other route of this controller.</param>
    /// <param name="keyId">The key's public id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 with <c>VALIDATION_ERROR</c> for a malformed id; 401 without a session; 403 with
    /// <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewModifyPasswords</c>; 404 with
    /// <c>SERVICE_NOT_FOUND</c> or <c>SSH_KEY_NOT_FOUND</c>; 409 with <c>SSH_KEY_STALE</c> or
    /// <c>SSH_ACCESS_UNAVAILABLE</c>; 429 when the key rate limit is exceeded; 503 with
    /// <c>SSH_SERVER_UNAVAILABLE</c> when the server did not confirm.
    /// </returns>
    [HttpDelete("{id:int}/ssh/keys/{keyId:guid}")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewModifyPasswords)]
    [EnableRateLimiting(RateLimitPolicies.SshKeys)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RevokeSshKeyAsync(int id, Guid keyId, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new RevokeMySshKeyCommand(id, keyId), ct);
        return HandleResult(outcome);
    }
}
