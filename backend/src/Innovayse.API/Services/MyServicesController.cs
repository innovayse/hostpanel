namespace Innovayse.API.Services;

using System.Security.Claims;
using Innovayse.API.Services.Requests;
using Innovayse.Application.Clients.DTOs;
using Innovayse.Application.Clients.Queries.GetMyProfile;
using Innovayse.Application.Provisioning.Queries.GetCPanelSsoUrl;
using Innovayse.Application.Services.Commands.CancelService;
using Innovayse.Application.Services.Commands.OrderService;
using Innovayse.Application.Services.Queries.GetCancellationStatus;
using Innovayse.Application.Services.Queries.GetMyServices;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Client portal endpoints for viewing and ordering services.
/// Requires Client role.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/me/services")]
[Authorize(Roles = Roles.Client)]
public sealed class MyServicesController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns all services belonging to the authenticated client.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of client service DTOs.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClientServiceDto>>> GetMineAsync(CancellationToken ct)
    {
        var userId = GetUserId();

        var profile = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(userId), ct);

        var services = await bus.InvokeAsync<IReadOnlyList<ClientServiceDto>>(
            new GetMyServicesQuery(profile.Id), ct);
        return Ok(services);
    }

    /// <summary>Orders a new service for the authenticated client.</summary>
    /// <param name="request">Order request body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new service ID.</returns>
    [HttpPost]
    public async Task<ActionResult<int>> OrderAsync(
        [FromBody] OrderRequest request, CancellationToken ct)
    {
        var userId = GetUserId();

        var profile = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(userId), ct);

        var cmd = new OrderServiceCommand(profile.Id, request.ProductId, request.BillingCycle);
        var id = await bus.InvokeAsync<int>(cmd, ct);
        return StatusCode(StatusCodes.Status201Created, id);
    }

    /// <summary>
    /// Generates a time-limited cPanel SSO URL for a service owned by the authenticated client.
    /// </summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with the cPanel SSO URL string.</returns>
    [HttpGet("{id:int}/cpanel-sso")]
    public async Task<ActionResult<string>> GetCPanelSsoUrlAsync(int id, CancellationToken ct)
    {
        var url = await bus.InvokeAsync<string>(new GetCPanelSsoUrlQuery(id), ct);
        return Ok(url);
    }

    /// <summary>Submits a cancellation request for a client service.</summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="request">Cancellation request body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK on success.</returns>
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> CancelAsync(
        int id, [FromBody] CancelServiceRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(
            new CancelServiceCommand(id, request.Type, request.Reason), ct);
        return Ok();
    }

    /// <summary>Returns the cancellation status for a client service.</summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with cancellation status DTO.</returns>
    [HttpGet("{id:int}/cancellation-status")]
    public async Task<ActionResult<CancellationStatusDto>> GetCancellationStatusAsync(
        int id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<CancellationStatusDto>(
            new GetCancellationStatusQuery(id), ct);
        return Ok(result);
    }

    /// <summary>Extracts the authenticated user's Identity ID from JWT claims.</summary>
    /// <returns>The user ID string.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the user ID claim is missing.</exception>
    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("User ID not found in token.");
}
