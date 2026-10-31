using Innovayse.Hostpanel.Application.Provisioning.Commands.ChangePackage;
using Innovayse.Hostpanel.Application.Provisioning.Commands.ChangePassword;
using Innovayse.Hostpanel.Application.Provisioning.Commands.ProvisionService;
using Innovayse.Hostpanel.Application.Provisioning.Common;
using Innovayse.Hostpanel.Application.Provisioning.Queries.GetControlPanelSsoUrl;
using Innovayse.Hostpanel.Application.Provisioning.Queries.GetServiceCredentials;
using Innovayse.Hostpanel.Application.Services.Commands.SuspendService;
using Innovayse.Hostpanel.Application.Services.Commands.TerminateService;
using Innovayse.Hostpanel.Application.Services.Commands.UnsuspendService;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Staff endpoints for managing a provisioned hosting service on its control-panel provider:
/// provisioning, suspension, termination, credentials and control-panel sign-in links.
/// </summary>
[Route("api/provisioning")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class ProvisioningController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public ProvisioningController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Provisions a pending hosting service on the configured provider.</summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 with <c>VALIDATION_ERROR</c> for a non-positive id, or with
    /// <c>SERVICE_NOT_PENDING</c>; 401 without a session; 403 for a caller who is not staff; 404 with <c>SERVICE_NOT_FOUND</c> when the id names no service; 409 with
    /// <c>SERVICE_HOSTING_DETAILS_MISSING</c>, <c>PRODUCT_PACKAGE_MISSING</c> or <c>PRODUCT_SERVER_GROUP_REQUIRED</c> when the service or its
    /// product is not ready to provision, or <c>PROVISIONING_ACCOUNT_CONFLICT</c> when the panel holds
    /// another account under the service's username; 502 with <c>PROVISIONING_UPSTREAM_FAILED</c> when the provider
    /// rejects the request; 503 with <c>NO_ELIGIBLE_SERVER</c> when no server has room;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/provision")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ProvisionAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new ProvisionServiceCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Suspends an active hosting service on the provider, then on the platform.</summary>
    /// <param name="id">Client service primary key; wins over any id in the body.</param>
    /// <param name="command">The reason for the suspension. A staff suspension is always manual, whatever the body says.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 with <c>VALIDATION_ERROR</c>, or with <c>SERVICE_NOT_ACTIVE</c> when the
    /// service is not Active; 401 without a session; 403 for a caller who is not staff; 404 with <c>SERVICE_NOT_FOUND</c> when the id names no service; 502 with
    /// <c>SERVICE_SUSPEND_UPSTREAM_FAILED</c> when the provider rejects the request;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/suspend")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> SuspendAsync(int id, [FromBody] SuspendServiceCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { ServiceId = id, Source = SuspensionSource.Manual }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Unsuspends a suspended hosting service on the provider, then on the platform.</summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 with <c>VALIDATION_ERROR</c>, or with <c>SERVICE_NOT_SUSPENDED</c> when the
    /// service is not Suspended; 401 without a session; 403 for a caller who is not staff; 404 with <c>SERVICE_NOT_FOUND</c> when the id names no service; 502 with
    /// <c>SERVICE_UNSUSPEND_UPSTREAM_FAILED</c> when the provider rejects the request;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/unsuspend")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> UnsuspendAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new UnsuspendServiceCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Permanently terminates a hosting service on the provider, then on the platform.</summary>
    /// <param name="id">Client service primary key; wins over any id in the body.</param>
    /// <param name="command">The reason for the termination.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 with <c>VALIDATION_ERROR</c>, or with <c>SERVICE_TERMINATE_NOT_ALLOWED</c>
    /// when the service is already terminated; 401 without a session; 403 for a caller who is not staff; 404 with <c>SERVICE_NOT_FOUND</c> when the id names no service; 502 with
    /// <c>SERVICE_TERMINATE_UPSTREAM_FAILED</c> when the provider rejects the request;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/terminate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> TerminateAsync(int id, [FromBody] TerminateServiceCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { ServiceId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Retrieves the current login credentials for a provisioned hosting service.</summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the credentials; 400 with <c>VALIDATION_ERROR</c> or <c>SERVICE_NOT_PROVISIONED</c>
    /// when the service has never been provisioned; 401 without a session; 403 for a caller who is not staff; 404 with <c>SERVICE_NOT_FOUND</c> when the id names no service;
    /// 502 with <c>SERVICE_CREDENTIALS_UPSTREAM_FAILED</c> when the provider cannot return them;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}/credentials")]
    [ProducesResponseType(typeof(ServiceCredentialsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetCredentialsAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<ServiceCredentialsDto>>(new GetServiceCredentialsQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Changes the hosting account password on the provisioning server.</summary>
    /// <param name="id">Client service primary key; wins over any id in the body.</param>
    /// <param name="command">The new password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 with <c>VALIDATION_ERROR</c>, <c>SERVICE_NOT_PROVISIONED</c> or
    /// <c>SERVICE_NO_SERVER_ASSIGNED</c>; 401 without a session; 403 for a caller who is not staff; 404 with <c>SERVICE_NOT_FOUND</c> when the id names no service; 502 with
    /// <c>SERVICE_PASSWORD_UPSTREAM_FAILED</c> when the provider rejects the change;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> ChangePasswordAsync(int id, [FromBody] ChangePasswordCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { ServiceId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Changes the hosting package on the provisioning server.</summary>
    /// <param name="id">Client service primary key; wins over any id in the body.</param>
    /// <param name="command">The new hosting package.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 with <c>VALIDATION_ERROR</c>, <c>SERVICE_NOT_PROVISIONED</c> or
    /// <c>SERVICE_NO_SERVER_ASSIGNED</c>; 401 without a session; 403 for a caller who is not staff; 404 with <c>SERVICE_NOT_FOUND</c> when the id names no service; 502 with
    /// <c>SERVICE_PACKAGE_UPSTREAM_FAILED</c> when the provider rejects the change;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/change-package")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> ChangePackageAsync(int id, [FromBody] ChangePackageCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { ServiceId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Generates a control-panel sign-in link for direct control panel access.</summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the control-panel sign-in link; 400 with <c>VALIDATION_ERROR</c>,
    /// <c>SERVICE_NOT_PROVISIONED</c>, <c>SERVICE_NO_SERVER_ASSIGNED</c> or, for a terminated service, <c>SERVICE_NOT_ACTIVE</c>; 401 without a session; 403 for a caller who is not staff; 404 with <c>SERVICE_NOT_FOUND</c> when the id
    /// names no service; 502 with <c>SERVICE_CPANEL_SSO_UPSTREAM_FAILED</c> when the provider cannot
    /// issue one;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}/cpanel-sso")]
    [ProducesResponseType(typeof(ControlPanelSsoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetCPanelSsoUrlAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<ControlPanelSsoDto>>(new GetControlPanelSsoUrlQuery(id), ct);
        return HandleResult(outcome);
    }
}
