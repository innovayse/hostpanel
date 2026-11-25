using Innovayse.Hostpanel.Application.Provisioning.Commands.ChangePackage;
using Innovayse.Hostpanel.Application.Provisioning.Commands.ChangePassword;
using Innovayse.Hostpanel.Application.Provisioning.Commands.ProvisionService;
using Innovayse.Hostpanel.Application.Provisioning.Commands.SyncServiceWithPanel;
using Innovayse.Hostpanel.Application.Provisioning.Common;
using Innovayse.Hostpanel.Application.Provisioning.Queries.GetPanelLoginDetails;
using Innovayse.Hostpanel.Application.Provisioning.Queries.GetPanelSsoUrl;
using Innovayse.Hostpanel.Application.Provisioning.Queries.GetServiceCredentials;
using Innovayse.Hostpanel.Application.Provisioning.Queries.ListServicePackages;
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
    /// product is not ready to provision, <c>CPANEL_USERNAME_INVALID</c> or <c>CPANEL_DOMAIN_INVALID</c> when the panel would never accept
    /// the stored username or domain, or <c>PROVISIONING_ACCOUNT_CONFLICT</c> when the panel holds
    /// another account under the service's username, or the panel's own refusal (<c>PANEL_PACKAGE_UNKNOWN</c>,
    /// <c>PANEL_USERNAME_INVALID</c>, <c>PANEL_DOMAIN_INVALID</c>, <c>PANEL_PASSWORD_REJECTED</c>, <c>PANEL_LIMIT_REACHED</c>) from the
    /// plugin that serves the server; 502 with <c>PROVISIONING_UPSTREAM_FAILED</c> when the provider
    /// rejects the request, or <c>PANEL_ACCESS_DENIED</c> or <c>PANEL_REFUSED</c> when the plugin does; 503 with <c>NO_ELIGIBLE_SERVER</c> when no server has room,
    /// <c>PROVISIONING_OUTCOME_UNKNOWN</c> when the provider could not tell whether the account was created and the call is to be retried,
    /// <c>PROVISIONING_PENDING</c> when the panel accepted the account and is still creating it (retry later), or
    /// <c>PANEL_PLUGIN_UNAVAILABLE</c> when the plugin that serves the server is switched off or not configured;
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
    /// 200 with the credentials; 400 with <c>VALIDATION_ERROR</c>, <c>SERVICE_NOT_PROVISIONED</c>
    /// when the service has never been provisioned, <c>SERVICE_NOT_ACTIVE</c> when it is terminated or
    /// <c>SERVICE_NO_SERVER_ASSIGNED</c> when it has no usable server; 401 without a session; 403 for a caller who is not staff; 404 with <c>SERVICE_NOT_FOUND</c> when the id names no service;
    /// 409 with a panel code (<c>PANEL_ACCOUNT_NOT_FOUND</c>, <c>PANEL_PACKAGE_UNKNOWN</c>, <c>PANEL_PASSWORD_REJECTED</c>, <c>PANEL_LIMIT_REACHED</c>, <c>PANEL_OPERATION_NOT_SUPPORTED</c> or <c>PANEL_PLUGIN_CONFLICT</c>) when the panel plugin that serves the server refuses the request; 502 with <c>PANEL_ACCESS_DENIED</c> or <c>PANEL_REFUSED</c> when the panel refuses it outright; 503 with <c>PANEL_PLUGIN_UNAVAILABLE</c> when that plugin is switched off or not configured; 
    /// 502 with <c>SERVICE_CREDENTIALS_UPSTREAM_FAILED</c> when the provider cannot return them;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}/credentials")]
    [ProducesResponseType(typeof(ServiceCredentialsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetCredentialsAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<ServiceCredentialsDto>>(new GetServiceCredentialsQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Lists the hosting packages the server of a provisioned service offers.</summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the package names; 400 with <c>VALIDATION_ERROR</c>, <c>SERVICE_NOT_PROVISIONED</c> when the service has never been
    /// provisioned, <c>SERVICE_NOT_ACTIVE</c> when it is terminated or <c>SERVICE_NO_SERVER_ASSIGNED</c> when it has no usable server;
    /// 401 without a session; 403 for a caller who is not staff; 404 with <c>SERVICE_NOT_FOUND</c> when the id names no service;
    /// 409 with a panel code (<c>PANEL_OPERATION_NOT_SUPPORTED</c> when the panel plugin cannot list its packages, or
    /// <c>PANEL_PLUGIN_CONFLICT</c>); 502 with <c>PANEL_ACCESS_DENIED</c>, <c>PANEL_REFUSED</c> or <c>SERVICE_PACKAGES_UPSTREAM_FAILED</c>;
    /// 503 with <c>PANEL_PLUGIN_UNAVAILABLE</c> when that plugin is switched off or not configured; 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}/packages")]
    [ProducesResponseType(typeof(ServicePackagesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ListPackagesAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<ServicePackagesDto>>(new ListServicePackagesQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Changes the hosting account password on the provisioning server.</summary>
    /// <param name="id">Client service primary key; wins over any id in the body.</param>
    /// <param name="command">The new password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 with <c>VALIDATION_ERROR</c>, <c>SERVICE_NOT_PROVISIONED</c>,
    /// <c>SERVICE_NOT_ACTIVE</c> for a terminated service or <c>SERVICE_NO_SERVER_ASSIGNED</c>; 401 without a session; 403 for a caller who is not staff; 404 with <c>SERVICE_NOT_FOUND</c> when the id names no service; 409 with a panel code (<c>PANEL_ACCOUNT_NOT_FOUND</c>, <c>PANEL_PACKAGE_UNKNOWN</c>, <c>PANEL_PASSWORD_REJECTED</c>, <c>PANEL_LIMIT_REACHED</c>, <c>PANEL_OPERATION_NOT_SUPPORTED</c> or <c>PANEL_PLUGIN_CONFLICT</c>) when the panel plugin that serves the server refuses the request; 502 with <c>PANEL_ACCESS_DENIED</c> or <c>PANEL_REFUSED</c> when the panel refuses it outright; 503 with <c>PANEL_PLUGIN_UNAVAILABLE</c> when that plugin is switched off or not configured; 
    /// 502 with <c>SERVICE_PASSWORD_UPSTREAM_FAILED</c> when the provider rejects the change;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
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
    /// 204 on success; 400 with <c>VALIDATION_ERROR</c>, <c>SERVICE_NOT_PROVISIONED</c>,
    /// <c>SERVICE_NOT_ACTIVE</c> for a terminated service or <c>SERVICE_NO_SERVER_ASSIGNED</c>; 401 without a session; 403 for a caller who is not staff; 404 with <c>SERVICE_NOT_FOUND</c> when the id names no service; 409 with a panel code (<c>PANEL_ACCOUNT_NOT_FOUND</c>, <c>PANEL_PACKAGE_UNKNOWN</c>, <c>PANEL_PASSWORD_REJECTED</c>, <c>PANEL_LIMIT_REACHED</c>, <c>PANEL_OPERATION_NOT_SUPPORTED</c> or <c>PANEL_PLUGIN_CONFLICT</c>) when the panel plugin that serves the server refuses the request; 502 with <c>PANEL_ACCESS_DENIED</c> or <c>PANEL_REFUSED</c> when the panel refuses it outright; 503 with <c>PANEL_PLUGIN_UNAVAILABLE</c> when that plugin is switched off or not configured; 
    /// 502 with <c>SERVICE_PACKAGE_UPSTREAM_FAILED</c> when the provider rejects the change;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/change-package")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ChangePackageAsync(int id, [FromBody] ChangePackageCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { ServiceId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Reconciles a provisioned hosting service with its control panel: the status and the package the platform holds are applied to
    /// the panel, the password too when asked, and the IP address the panel reports is recorded on the service.
    /// </summary>
    /// <param name="id">Client service primary key; wins over any id in the body.</param>
    /// <param name="command">Whether the stored password is also set on the panel.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with what was done about each field; 400 with <c>VALIDATION_ERROR</c>, <c>SERVICE_NOT_PROVISIONED</c>,
    /// <c>SERVICE_NOT_ACTIVE</c> for a terminated service or <c>SERVICE_NO_SERVER_ASSIGNED</c>; 401 without a session; 403 for a caller who
    /// is not staff; 404 with <c>SERVICE_NOT_FOUND</c> when the id names no service; 409 with a panel code (<c>PANEL_ACCOUNT_NOT_FOUND</c>,
    /// <c>PANEL_OPERATION_NOT_SUPPORTED</c> or <c>PANEL_PLUGIN_CONFLICT</c>) when the panel plugin that serves the server cannot read the
    /// account; 502 with <c>PANEL_ACCESS_DENIED</c>, <c>PANEL_REFUSED</c> or <c>SERVICE_PANEL_SYNC_UPSTREAM_FAILED</c> when the panel cannot be
    /// read; 503 with <c>PANEL_PLUGIN_UNAVAILABLE</c> when that plugin is switched off or not configured; 429 when the rate limit is hit.
    /// A change that fails to apply is reported in its field of the 200 answer.
    /// </returns>
    [HttpPost("/api/admin/services/{id:int}/sync-panel")]
    [ProducesResponseType(typeof(ServiceSyncDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SyncWithPanelAsync(int id, [FromBody] SyncServiceWithPanelCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<ServiceSyncDto>>(command with { ServiceId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Generates a control-panel sign-in link for direct control panel access.</summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the control-panel sign-in link; 400 with <c>VALIDATION_ERROR</c>,
    /// <c>SERVICE_NOT_PROVISIONED</c>, <c>SERVICE_NO_SERVER_ASSIGNED</c> or, for a terminated service, <c>SERVICE_NOT_ACTIVE</c>; 401 without a session; 403 for a caller who is not staff; 404 with <c>SERVICE_NOT_FOUND</c> when the id
    /// names no service; 409 with a panel code (<c>PANEL_ACCOUNT_NOT_FOUND</c>, <c>PANEL_PACKAGE_UNKNOWN</c>, <c>PANEL_PASSWORD_REJECTED</c>, <c>PANEL_LIMIT_REACHED</c>, <c>PANEL_OPERATION_NOT_SUPPORTED</c> or <c>PANEL_PLUGIN_CONFLICT</c>) when the panel plugin that serves the server refuses the request; 502 with <c>PANEL_ACCESS_DENIED</c> or <c>PANEL_REFUSED</c> when the panel refuses it outright; 503 with <c>PANEL_PLUGIN_UNAVAILABLE</c> when that plugin is switched off or not configured; 
    /// 502 with <c>SERVICE_PANEL_SSO_UPSTREAM_FAILED</c> when the provider cannot
    /// issue one, or <c>SERVICE_PANEL_SSO_URL_UPSTREAM_FAILED</c> when the panel answered an address that is not on its own server;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}/panel-sso")]
    [ProducesResponseType(typeof(PanelSsoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetPanelSsoUrlAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PanelSsoDto>>(new GetPanelSsoUrlQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Reads how a person signs in to the panel of a service by hand (the panel's sign in page, the user name, the domain and the
    /// server), for a panel that cannot issue a single sign on link. It never carries a password.
    /// </summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the login details; 400 with <c>VALIDATION_ERROR</c>, <c>SERVICE_NOT_PROVISIONED</c>, <c>SERVICE_NO_SERVER_ASSIGNED</c> or,
    /// for a terminated service, <c>SERVICE_NOT_ACTIVE</c>; 401 without a session; 403 for a caller who is not staff; 404 with
    /// <c>SERVICE_NOT_FOUND</c> when the id names no service; 409 with a panel code (<c>PANEL_OPERATION_NOT_SUPPORTED</c> when the plugin
    /// has no login capability, or another panel refusal code) when the panel plugin that serves the server refuses the request; 502 with
    /// <c>PANEL_ACCESS_DENIED</c>, <c>PANEL_REFUSED</c> or <c>SERVICE_CREDENTIALS_UPSTREAM_FAILED</c>; 503 with <c>PANEL_PLUGIN_UNAVAILABLE</c>
    /// when that plugin is switched off or not configured; 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}/panel-login")]
    [ProducesResponseType(typeof(PanelLoginDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetPanelLoginDetailsAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PanelLoginDetailsDto>>(new GetPanelLoginDetailsQuery(id), ct);
        return HandleResult(outcome);
    }
}
