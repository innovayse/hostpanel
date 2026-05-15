namespace Innovayse.API.Services;

using Innovayse.Application.Common;
using Innovayse.Application.Services.Commands.SuspendService;
using Innovayse.Application.Services.Commands.TerminateService;
using Innovayse.Application.Services.Commands.UnsuspendService;
using Innovayse.Application.Services.Commands.UpdateService;
using Innovayse.Application.Services.DTOs;
using Innovayse.Application.Services.Queries.GetService;
using Innovayse.Application.Services.Queries.GetServices;
using Innovayse.API.Services.Requests;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing all client services.
/// Requires Admin or Reseller role.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/services")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Reseller}")]
public sealed class ServicesController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns a paginated list of all client services with enriched data.</summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Items per page (max 100).</param>
    /// <param name="clientId">Optional client ID filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged list of enriched service list item DTOs.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ServiceListItemDto>>> GetAllAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? clientId = null,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<PagedResult<ServiceListItemDto>>(
            new GetServicesQuery(page, pageSize, clientId), ct);
        return Ok(result);
    }

    /// <summary>Returns a single client service with full detail.</summary>
    /// <param name="id">Service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Enriched service detail DTO.</returns>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ServiceDetailDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<ServiceDetailDto>(
            new GetServiceQuery(id), ct);
        return Ok(result);
    }

    /// <summary>Updates an existing client service.</summary>
    /// <param name="id">Service primary key.</param>
    /// <param name="req">Request body with editable fields.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateServiceRequest req, CancellationToken ct)
    {
        await bus.InvokeAsync(new UpdateServiceCommand(
            id,
            req.Domain,
            req.DedicatedIp,
            req.Username,
            req.Password,
            req.BillingCycle,
            req.RecurringAmount,
            req.PaymentMethod,
            req.NextRenewalAt,
            req.SubscriptionId,
            req.OverrideAutoSuspend,
            req.SuspendUntil,
            req.AutoTerminateEndOfCycle,
            req.AutoTerminateReason,
            req.AdminNotes,
            req.ProvisioningRef,
            req.FirstPaymentAmount,
            req.PromotionCode,
            req.TerminatedAt,
            req.Status), ct);
        return NoContent();
    }

    /// <summary>Suspends an active client service.</summary>
    /// <param name="id">Service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id:int}/suspend")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> SuspendAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new SuspendServiceCommand(id), ct);
        return NoContent();
    }

    /// <summary>Permanently terminates a client service.</summary>
    /// <param name="id">Service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id:int}/terminate")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> TerminateAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new TerminateServiceCommand(id), ct);
        return NoContent();
    }

    /// <summary>Re-activates a previously suspended client service.</summary>
    /// <param name="id">Service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id:int}/unsuspend")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> UnsuspendAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new UnsuspendServiceCommand(id), ct);
        return NoContent();
    }
}
