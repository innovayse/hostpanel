namespace Innovayse.API.Billing;

using System.Security.Claims;
using Innovayse.API.Billing.Requests;
using Innovayse.Application.Billing.Commands.PayInvoice;
using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Billing.Queries.GetInvoice;
using Innovayse.Application.Billing.Queries.GetMyInvoices;
using Innovayse.Application.Clients.DTOs;
using Innovayse.Application.Clients.Queries.GetMyProfile;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Client portal endpoints for viewing and paying the authenticated client's invoices.
/// Requires Client role.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/me/invoices")]
[Authorize(Roles = Roles.Client)]
public sealed class MyBillingController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns all invoices belonging to the authenticated client.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of invoice DTOs with line items.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvoiceDto>>> GetMineAsync(CancellationToken ct)
    {
        var userId = GetUserId();
        var profile = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(userId), ct);
        var invoices = await bus.InvokeAsync<IReadOnlyList<InvoiceDto>>(new GetMyInvoicesQuery(profile.Id), ct);
        return Ok(invoices);
    }

    /// <summary>Returns a single invoice belonging to the authenticated client.</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The invoice DTO if it belongs to the client; 403 Forbidden otherwise.</returns>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<InvoiceDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        var profile = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(userId), ct);
        var invoice = await bus.InvokeAsync<InvoiceDto>(new GetInvoiceQuery(id), ct);

        if (invoice.ClientId != profile.Id)
        {
            return Forbid();
        }

        return Ok(invoice);
    }

    /// <summary>Pays an invoice belonging to the authenticated client.</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="request">Payment request (currency).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success; 403 Forbidden if the invoice does not belong to the client.</returns>
    [HttpPost("{id:int}/pay")]
    public async Task<IActionResult> PayAsync(int id, [FromBody] PayInvoiceRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var profile = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(userId), ct);

        var invoice = await bus.InvokeAsync<InvoiceDto>(new GetInvoiceQuery(id), ct);
        if (invoice.ClientId != profile.Id)
        {
            return Forbid();
        }

        await bus.InvokeAsync(new PayInvoiceCommand(id, request.Currency), ct);
        return NoContent();
    }

    /// <summary>Fallback JWT claim type when <see cref="ClaimTypes.NameIdentifier"/> is absent.</summary>
    private const string SubClaimType = "sub";

    /// <summary>Extracts the authenticated user's Identity ID from JWT claims.</summary>
    /// <returns>The user ID string.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the user ID claim is missing.</exception>
    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(SubClaimType)
            ?? throw new UnauthorizedAccessException("User ID not found in token.");
}
