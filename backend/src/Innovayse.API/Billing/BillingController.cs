namespace Innovayse.API.Billing;

using Innovayse.API.Billing.Requests;
using Innovayse.Application.Billing.Commands.AddInvoicePayment;
using Innovayse.Application.Billing.Commands.ApplyInvoiceCredit;
using Innovayse.Application.Billing.Commands.BulkInvoiceAction;
using Innovayse.Application.Billing.Commands.CancelInvoice;
using Innovayse.Application.Billing.Commands.CreateInvoice;
using Innovayse.Application.Billing.Commands.DeleteInvoice;
using Innovayse.Application.Billing.Commands.DuplicateInvoice;
using Innovayse.Application.Billing.Commands.MarkInvoiceOverdue;
using Innovayse.Application.Billing.Commands.PayInvoice;
using Innovayse.Application.Billing.Commands.PublishInvoice;
using Innovayse.Application.Billing.Commands.RefundInvoicePayment;
using Innovayse.Application.Billing.Commands.RemoveInvoiceCredit;
using Innovayse.Application.Billing.Commands.UpdateInvoiceItems;
using Innovayse.Application.Billing.Commands.UpdateInvoiceNotes;
using Innovayse.Application.Billing.Commands.UpdateInvoiceOptions;
using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Billing.Queries.GetInvoice;
using Innovayse.Application.Billing.Queries.ListClientInvoices;
using Innovayse.Application.Billing.Queries.ListInvoices;
using Innovayse.Application.Common;
using Innovayse.Domain.Auth;
using Innovayse.Domain.Billing;
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
    /// <summary>Returns a paginated, optionally filtered list of all invoices.</summary>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page (default 20, max 100).</param>
    /// <param name="status">Optional status filter (Paid, Unpaid, Overdue, Draft, Cancelled, Refunded, Collections, PaymentPending).</param>
    /// <param name="from">Optional start date filter (inclusive, UTC).</param>
    /// <param name="to">Optional end date filter (inclusive, UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated invoice list.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResult<InvoiceListItemDto>>> GetAllAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<PagedResult<InvoiceListItemDto>>(
            new ListInvoicesQuery(page, pageSize, status, from, to), ct);
        return Ok(result);
    }

    /// <summary>Returns a paginated, filtered list of invoices for a specific client.</summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page (default 20, max 100).</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="from">Optional start date filter (inclusive, UTC).</param>
    /// <param name="to">Optional end date filter (inclusive, UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated invoice list for the client.</returns>
    [HttpGet("client/{clientId:int}")]
    public async Task<ActionResult<PagedResult<InvoiceListItemDto>>> GetByClientAsync(
        int clientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] InvoiceStatus? status = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<PagedResult<InvoiceListItemDto>>(
            new ListClientInvoicesQuery(clientId, page, pageSize, status, from, to), ct);
        return Ok(result);
    }

    /// <summary>Returns a single invoice with its line items and transactions.</summary>
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

        var cmd = new CreateInvoiceCommand(request.ClientId, request.DueDate, items, request.IsDraft);
        var id = await bus.InvokeAsync<int>(cmd, ct);
        return StatusCode(StatusCodes.Status201Created, id);
    }

    /// <summary>Updates line items on an invoice (add, update, delete).</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="request">Item changes request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPut("{id:int}/items")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> UpdateItemsAsync(
        int id, [FromBody] UpdateInvoiceItemsRequest request, CancellationToken ct)
    {
        var entries = request.Items
            .Select(i => new UpdateItemEntry(i.Id, i.Description, i.UnitPrice, i.Quantity, i.IsDeleted))
            .ToList();

        await bus.InvokeAsync(new UpdateInvoiceItemsCommand(id, entries), ct);
        return NoContent();
    }

    /// <summary>Updates invoice options (dates, payment method, tax rate).</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="request">Options update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPut("{id:int}/options")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> UpdateOptionsAsync(
        int id, [FromBody] UpdateInvoiceOptionsRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new UpdateInvoiceOptionsCommand(
            id, request.InvoiceDate, request.DueDate, request.PaymentMethod, request.TaxRate), ct);
        return NoContent();
    }

    /// <summary>Updates or clears invoice notes.</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="request">Notes update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPut("{id:int}/notes")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> UpdateNotesAsync(
        int id, [FromBody] UpdateInvoiceNotesRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new UpdateInvoiceNotesCommand(id, request.Notes), ct);
        return NoContent();
    }

    /// <summary>Publishes a draft invoice, making it payable.</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id:int}/publish")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> PublishAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new PublishInvoiceCommand(id), ct);
        return NoContent();
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

    /// <summary>Records a manual payment against an invoice.</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="request">Payment details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id:int}/payment")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> AddPaymentAsync(
        int id, [FromBody] AddInvoicePaymentRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new AddInvoicePaymentCommand(
            id, request.Date, request.Gateway, request.TransactionId, request.Amount, request.Fees, request.Notes), ct);
        return NoContent();
    }

    /// <summary>Applies a credit to an invoice.</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="request">Credit details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id:int}/credit")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> ApplyCreditAsync(
        int id, [FromBody] ApplyInvoiceCreditRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new ApplyInvoiceCreditCommand(id, request.Amount), ct);
        return NoContent();
    }

    /// <summary>Removes a specific credit amount from an invoice.</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="request">Credit amount to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id:int}/credit/remove")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> RemoveCreditAsync(
        int id, [FromBody] ApplyInvoiceCreditRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new RemoveInvoiceCreditCommand(id, request.Amount), ct);
        return NoContent();
    }

    /// <summary>Refunds a paid invoice.</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="request">Refund details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id:int}/refund")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> RefundAsync(
        int id, [FromBody] RefundInvoicePaymentRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new RefundInvoicePaymentCommand(
            id, request.Amount, request.RefundType, request.Gateway, request.TransactionId,
            request.RefundTransactionId, request.Notes), ct);
        return NoContent();
    }

    /// <summary>Duplicates an invoice as a new draft.</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new draft invoice ID.</returns>
    [HttpPost("{id:int}/duplicate")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<int>> DuplicateAsync(int id, CancellationToken ct)
    {
        var newId = await bus.InvokeAsync<int>(new DuplicateInvoiceCommand(id), ct);
        return StatusCode(StatusCodes.Status201Created, newId);
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

    /// <summary>Deletes a draft or cancelled invoice.</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteInvoiceCommand(id), ct);
        return NoContent();
    }

    /// <summary>Performs a bulk action on multiple invoices.</summary>
    /// <param name="request">Bulk action request with invoice IDs and action name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The count of affected invoices.</returns>
    [HttpPost("bulk")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<int>> BulkActionAsync(
        [FromBody] BulkInvoiceActionRequest request, CancellationToken ct)
    {
        var count = await bus.InvokeAsync<int>(
            new BulkInvoiceActionCommand(request.InvoiceIds, request.Action), ct);
        return Ok(count);
    }
}
