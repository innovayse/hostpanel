using Innovayse.Hostpanel.Application.Services.Commands.AddExistingService;
using Innovayse.Hostpanel.Application.Services.Commands.ChangeServiceStatus;
using Innovayse.Hostpanel.Application.Services.Commands.CorrectServiceTerminationDate;
using Innovayse.Hostpanel.Application.Services.Commands.SuspendService;
using Innovayse.Hostpanel.Application.Services.Commands.TerminateService;
using Innovayse.Hostpanel.Application.Services.Commands.UnsuspendService;
using Innovayse.Hostpanel.Application.Services.Commands.UpdateServiceAutomation;
using Innovayse.Hostpanel.Application.Services.Commands.UpdateServiceBillingTerms;
using Innovayse.Hostpanel.Application.Services.Commands.UpdateServiceHostingAccount;
using Innovayse.Hostpanel.Application.Services.Commands.UpdateServiceNotes;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Application.Services.Queries.GetService;
using Innovayse.Hostpanel.Application.Services.Queries.GetServices;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>Admin endpoints for managing all client services.</summary>
[Route("api/services")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class ServicesController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public ServicesController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns a paginated list of all client services with enriched data.</summary>
    /// <param name="query">Page, page size and optional client filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the page; 400 when a value is refused; 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(PaginatedList<ServiceListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllAsync([FromQuery] GetServicesQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<ServiceListItemDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns a single client service with full detail.</summary>
    /// <param name="id">Service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the detail; 400 when a value is refused; 404 when the id names no service;
    /// 401 when the caller is not signed in; 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(ServiceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByIdAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<ServiceDetailDto>>(new GetServiceQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Changes what a client service is billed: product, quantity, cycle, amounts, next renewal date and payment references.</summary>
    /// <param name="id">Service primary key; wins over any id in the body.</param>
    /// <param name="command">The new billing terms.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when written; 400 when a field is refused; 404 when the id names no service or the product does not exist;
    /// 401 when the caller is not signed in; 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}/billing")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateBillingTermsAsync(int id, [FromBody] UpdateServiceBillingTermsCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { ServiceId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Adds a service to a client for a hosting account that already exists on a server. The service
    /// starts Active; nothing is created on the panel and no welcome mail is sent.
    /// </summary>
    /// <param name="command">The client, product, billing terms and the existing account.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new service's id; 400 when a field is refused; 404 with <c>CLIENT_NOT_FOUND</c>,
    /// <c>PRODUCT_NOT_FOUND</c> or <c>SERVER_NOT_FOUND</c>; 409 with <c>SERVICE_HOSTING_ACCOUNT_EXISTS</c>
    /// when a live service already holds that username on that server; 401 when the caller is not
    /// signed in; 403 when the caller's role is not allowed; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("existing")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddExistingAsync([FromBody] AddExistingServiceCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/services/{id}", id));
    }

    /// <summary>Corrects the record of the hosting account a client service lives on; nothing changes on the panel.</summary>
    /// <param name="id">Service primary key; wins over any id in the body.</param>
    /// <param name="command">The hosting account details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when written; 400 when a field is refused; 404 when the id names no service;
    /// 401 when the caller is not signed in; 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}/hosting-account")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateHostingAccountAsync(int id, [FromBody] UpdateServiceHostingAccountCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { ServiceId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Sets the suspension hold and the end-of-cycle termination of a client service.</summary>
    /// <param name="id">Service primary key; wins over any id in the body.</param>
    /// <param name="command">The automation settings.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when written; 400 when a field is refused or a date or reason is given without its switch;
    /// 404 when the id names no service; 401 when the caller is not signed in; 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}/automation")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateAutomationAsync(int id, [FromBody] UpdateServiceAutomationCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { ServiceId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Replaces the internal notes kept on a client service.</summary>
    /// <param name="id">Service primary key; wins over any id in the body.</param>
    /// <param name="command">The new notes.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when written; 400 when the notes are refused; 404 when the id names no service;
    /// 401 when the caller is not signed in; 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}/notes")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateNotesAsync(int id, [FromBody] UpdateServiceNotesCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { ServiceId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Moves a client service to another status through the matching transition.</summary>
    /// <param name="id">Service primary key; wins over any id in the body.</param>
    /// <param name="command">The status asked for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when moved; 400 when the status is unknown, or with <c>SERVICE_STATUS_CHANGE_NOT_ALLOWED</c>
    /// when the service cannot be moved to it; 404 when the id names no service; 502 with the
    /// transition's <c>*_UPSTREAM_FAILED</c> code when the panel refuses; 401 when the caller is not
    /// signed in; 403 when the caller's role is not allowed; 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}/status")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> ChangeStatusAsync(int id, [FromBody] ChangeServiceStatusCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { ServiceId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Corrects the recorded termination date of a terminated client service.</summary>
    /// <param name="id">Service primary key; wins over any id in the body.</param>
    /// <param name="command">The moment the service really ended.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when written; 400 when the date is missing; 404 when the id names no service;
    /// 409 when the service is not terminated; 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}/termination-date")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CorrectTerminationDateAsync(int id, [FromBody] CorrectServiceTerminationDateCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { ServiceId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Suspends an active client service.</summary>
    /// <param name="id">Service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when suspended; 400 when a value is refused, or with <c>SERVICE_NOT_ACTIVE</c> when the service is not Active;
    /// 404 when the id names no service; 502 with <c>SERVICE_SUSPEND_UPSTREAM_FAILED</c> when the panel refuses;
    /// 401 when the caller is not signed in; 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/suspend")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> SuspendAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new SuspendServiceCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Permanently terminates a client service.</summary>
    /// <param name="id">Service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when terminated; 400 when a value is refused, or with <c>SERVICE_TERMINATE_NOT_ALLOWED</c> when the service is already terminated;
    /// 404 when the id names no service; 502 with <c>SERVICE_TERMINATE_UPSTREAM_FAILED</c> when the panel refuses;
    /// 401 when the caller is not signed in; 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/terminate")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> TerminateAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new TerminateServiceCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Re-activates a previously suspended client service.</summary>
    /// <param name="id">Service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when unsuspended; 400 when a value is refused, or with <c>SERVICE_NOT_SUSPENDED</c> when the service is not Suspended;
    /// 404 when the id names no service; 502 with <c>SERVICE_UNSUSPEND_UPSTREAM_FAILED</c> when the panel refuses;
    /// 401 when the caller is not signed in; 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/unsuspend")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> UnsuspendAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new UnsuspendServiceCommand(id), ct);
        return HandleResult(outcome);
    }
}
