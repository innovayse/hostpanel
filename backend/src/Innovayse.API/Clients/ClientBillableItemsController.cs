namespace Innovayse.API.Clients;

using Innovayse.API.Billing.Requests;
using Innovayse.Application.Billing.Commands.CreateBillableItem;
using Innovayse.Application.Billing.Commands.CreateTimeBillingEntries;
using Innovayse.Application.Billing.Commands.DeleteBillableItem;
using Innovayse.Application.Billing.Commands.InvoiceSelectedItems;
using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Billing.Queries.ListClientBillableItems;
using Innovayse.Domain.Auth;
using Innovayse.Domain.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Client-scoped endpoints for managing billable items within a client profile.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/clients/{clientId:int}/billable-items")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Reseller}")]
public sealed class ClientBillableItemsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns uninvoiced and paginated invoiced billable items for a client.</summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="invoicedPage">1-based page number for invoiced items (default 1).</param>
    /// <param name="invoicedPageSize">Items per page for invoiced items (default 20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Billable items result DTO.</returns>
    [HttpGet]
    public async Task<ActionResult<ClientBillableItemsResultDto>> GetAllAsync(
        int clientId,
        [FromQuery] int invoicedPage = 1,
        [FromQuery] int invoicedPageSize = 20,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<ClientBillableItemsResultDto>(
            new ListClientBillableItemsQuery(clientId, invoicedPage, invoicedPageSize), ct);
        return Ok(result);
    }

    /// <summary>Creates a new billable item for a client.</summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="request">Billable item creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new billable item ID.</returns>
    [HttpPost]
    public async Task<ActionResult<int>> CreateAsync(
        int clientId,
        [FromBody] CreateBillableItemRequest request,
        CancellationToken ct)
    {
        var cmd = new CreateBillableItemCommand(
            clientId,
            request.Description,
            request.Amount,
            request.Currency,
            request.Type,
            request.RecurringPeriod,
            request.NextDueDate);

        var id = await bus.InvokeAsync<int>(cmd, ct);
        return StatusCode(StatusCodes.Status201Created, id);
    }

    /// <summary>Creates multiple time billing entries as billable items.</summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="request">Time billing entries request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with a list of new billable item IDs.</returns>
    [HttpPost("time-billing")]
    public async Task<ActionResult<IReadOnlyList<int>>> CreateTimeBillingAsync(
        int clientId,
        [FromBody] CreateTimeBillingRequest request,
        CancellationToken ct)
    {
        var entries = request.Entries
            .Select(e => new TimeBillingEntry(e.ServiceId, e.Description, e.Hours, e.Rate))
            .ToList();

        var cmd = new CreateTimeBillingEntriesCommand(clientId, entries);
        var ids = await bus.InvokeAsync<IReadOnlyList<int>>(cmd, ct);
        return StatusCode(StatusCodes.Status201Created, ids);
    }

    /// <summary>Creates an invoice from selected uninvoiced billable items.</summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="request">Selected item IDs request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with the new invoice ID.</returns>
    [HttpPost("invoice-selected")]
    public async Task<ActionResult<int>> InvoiceSelectedAsync(
        int clientId,
        [FromBody] InvoiceSelectedItemsRequest request,
        CancellationToken ct)
    {
        var cmd = new InvoiceSelectedItemsCommand(clientId, request.BillableItemIds);
        var invoiceId = await bus.InvokeAsync<int>(cmd, ct);
        return Ok(invoiceId);
    }

    /// <summary>Deletes an uninvoiced billable item.</summary>
    /// <param name="clientId">The client's primary key (route parameter).</param>
    /// <param name="id">The billable item primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int clientId, int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteBillableItemCommand(id), ct);
        return NoContent();
    }
}
