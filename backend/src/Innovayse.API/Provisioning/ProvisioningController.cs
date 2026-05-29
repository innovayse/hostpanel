namespace Innovayse.API.Provisioning;

using Innovayse.Application.Provisioning.Commands.ChangePackage;
using Innovayse.Application.Provisioning.Commands.ChangePassword;
using Innovayse.Application.Provisioning.Commands.ProvisionService;
using Innovayse.Application.Provisioning.Commands.SuspendService;
using Innovayse.Application.Provisioning.Commands.TerminateService;
using Innovayse.Application.Provisioning.Commands.UnsuspendService;
using Innovayse.Application.Provisioning.DTOs;
using Innovayse.Application.Provisioning.Queries.GetCPanelSsoUrl;
using Innovayse.Application.Provisioning.Queries.GetServiceCredentials;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin and Reseller endpoints for managing provisioned hosting services.
/// All actions dispatch commands or queries via the Wolverine message bus.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/provisioning")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Reseller}")]
public sealed class ProvisioningController(IMessageBus bus) : ControllerBase
{
    /// <summary>Provisions a pending hosting service on the configured provider.</summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK on success.</returns>
    [HttpPost("{id:int}/provision")]
    public async Task<IActionResult> ProvisionAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new ProvisionServiceCommand(id), ct);
        return Ok();
    }

    /// <summary>Suspends an active hosting service on the provider.</summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="request">Suspension request body containing the reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK on success.</returns>
    [HttpPost("{id:int}/suspend")]
    public async Task<IActionResult> SuspendAsync(
        int id,
        [FromBody] SuspendServiceRequest request,
        CancellationToken ct)
    {
        await bus.InvokeAsync(new SuspendServiceCommand(id, request.Reason), ct);
        return Ok();
    }

    /// <summary>Unsuspends a suspended hosting service on the provider.</summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK on success.</returns>
    [HttpPost("{id:int}/unsuspend")]
    public async Task<IActionResult> UnsuspendAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new UnsuspendServiceCommand(id), ct);
        return Ok();
    }

    /// <summary>Permanently terminates a hosting service on the provider.</summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="request">Termination request body containing the reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK on success.</returns>
    [HttpPost("{id:int}/terminate")]
    public async Task<IActionResult> TerminateAsync(
        int id,
        [FromBody] TerminateServiceRequest request,
        CancellationToken ct)
    {
        await bus.InvokeAsync(new TerminateServiceCommand(id, request.Reason), ct);
        return Ok();
    }

    /// <summary>Retrieves the current login credentials for a provisioned hosting service.</summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with <see cref="ServiceCredentialsDto"/>.</returns>
    [HttpGet("{id:int}/credentials")]
    public async Task<ActionResult<ServiceCredentialsDto>> GetCredentialsAsync(
        int id,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<ServiceCredentialsDto>(
            new GetServiceCredentialsQuery(id), ct);
        return Ok(result);
    }

    /// <summary>Changes the hosting account password on the provisioning server.</summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="request">Request body containing the new password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK on success.</returns>
    [HttpPost("{id:int}/change-password")]
    public async Task<IActionResult> ChangePasswordAsync(
        int id,
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct)
    {
        await bus.InvokeAsync(new ChangePasswordCommand(id, request.NewPassword), ct);
        return Ok();
    }

    /// <summary>Changes the hosting package on the provisioning server.</summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="request">Request body containing the new package name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK on success.</returns>
    [HttpPost("{id:int}/change-package")]
    public async Task<IActionResult> ChangePackageAsync(
        int id,
        [FromBody] ChangePackageRequest request,
        CancellationToken ct)
    {
        await bus.InvokeAsync(new ChangePackageCommand(id, request.NewPackage), ct);
        return Ok();
    }

    /// <summary>Generates a single-sign-on URL for direct cPanel access.</summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with a JSON object containing the SSO URL.</returns>
    [HttpGet("{id:int}/cpanel-sso")]
    public async Task<IActionResult> GetCPanelSsoUrlAsync(int id, CancellationToken ct)
    {
        var url = await bus.InvokeAsync<string>(new GetCPanelSsoUrlQuery(id), ct);
        return Ok(new { url });
    }
}
