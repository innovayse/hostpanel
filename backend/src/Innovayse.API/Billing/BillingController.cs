namespace Innovayse.API.Billing;

using Innovayse.API.Billing.Requests;
using Innovayse.Application.Billing.Commands.CancelInvoice;
using Innovayse.Application.Billing.Commands.CreateInvoice;
using Innovayse.Application.Billing.Commands.MarkInvoiceOverdue;
using Innovayse.Application.Billing.Commands.PayInvoice;
using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Billing.Queries.GetInvoice;
using Innovayse.Application.Billing.Queries.ListInvoices;
using Innovayse.Application.Common;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin and Reseller endpoints for managing invoices.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/billing")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Reseller}")]
public sealed class BillingController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns a paginated list of all invoices.</summary>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page (default 20, max 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated invoice list.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResult<InvoiceListItemDto>>> GetAllAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<PagedResult<InvoiceListItemDto>>(
            new ListInvoicesQuery(page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Returns a single invoice with its line items.</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Invoice DTO.</returns>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<InvoiceDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var dto = await bus.InvokeAsync<InvoiceDto>(new GetInvoiceQuery(id), ct);
        return Ok(dto);
    }

    /// <summary>Creates a new invoice for a client.</summary>
    /// <param name="request">Invoice creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new invoice ID.</returns>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<int>> CreateAsync(
        [FromBody] CreateInvoiceRequest request, CancellationToken ct)
    {
        var items = request.Items
            .Select(i => new InvoiceItemRequest(i.Description, i.UnitPrice, i.Quantity))
            .ToList();

        var cmd = new CreateInvoiceCommand(request.ClientId, request.DueDate, items);
        var id = await bus.InvokeAsync<int>(cmd, ct);
        return StatusCode(StatusCodes.Status201Created, id);
    }

    /// <summary>Charges the client and marks the invoice as paid.</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="request">Payment request (currency).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id:int}/pay")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> PayAsync(int id, [FromBody] PayInvoiceRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new PayInvoiceCommand(id, request.Currency), ct);
        return NoContent();
    }

    /// <summary>Cancels an invoice.</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> CancelAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new CancelInvoiceCommand(id), ct);
        return NoContent();
    }

    /// <summary>Marks an invoice as overdue.</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id:int}/mark-overdue")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> MarkOverdueAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new MarkInvoiceOverdueCommand(id), ct);
        return NoContent();
    }
}
