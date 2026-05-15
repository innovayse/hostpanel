namespace Innovayse.API.Provisioning;

using Innovayse.Application.Provisioning.Commands.ProvisionService;
using Innovayse.Application.Provisioning.Commands.SuspendService;
using Innovayse.Application.Provisioning.Commands.TerminateService;
using Innovayse.Application.Provisioning.Commands.UnsuspendService;
using Innovayse.Application.Provisioning.DTOs;
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
}
