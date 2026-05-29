namespace Innovayse.API.Billing;

using Innovayse.API.Billing.Requests;
<<<<<<< HEAD
using Innovayse.Application.Billing.Commands.CreateQuote;
using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Billing.Queries.GetQuote;
using Innovayse.Application.Billing.Queries.ListQuotes;
=======
using Innovayse.Application.Billing.Commands.ConvertQuoteToInvoice;
using Innovayse.Application.Billing.Commands.CreateQuote;
using Innovayse.Application.Billing.Commands.DeleteQuote;
using Innovayse.Application.Billing.Commands.DuplicateQuote;
using Innovayse.Application.Billing.Commands.UpdateQuote;
using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Billing.Queries.GetQuote;
using Innovayse.Application.Billing.Queries.ListClientQuotes;
>>>>>>> origin/main
using Innovayse.Application.Common;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
<<<<<<< HEAD
/// Admin endpoints for managing quotes.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/billing/quotes")]
[Authorize(Roles = Roles.Admin)]
public sealed class QuotesController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns a paginated list of all quotes.</summary>
=======
/// Admin and Reseller endpoints for managing quotes (proposals).
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/quotes")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Reseller}")]
public sealed class QuotesController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns a paginated list of quotes for a specific client.</summary>
    /// <param name="clientId">The client's primary key.</param>
>>>>>>> origin/main
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page (default 20, max 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated quote list.</returns>
<<<<<<< HEAD
    [HttpGet]
    public async Task<ActionResult<PagedResult<QuoteListItemDto>>> GetAllAsync(
=======
    [HttpGet("client/{clientId:int}")]
    public async Task<ActionResult<PagedResult<QuoteListItemDto>>> GetByClientAsync(
        int clientId,
>>>>>>> origin/main
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<PagedResult<QuoteListItemDto>>(
<<<<<<< HEAD
            new ListQuotesQuery(page, pageSize), ct);
=======
            new ListClientQuotesQuery(clientId, page, pageSize), ct);
>>>>>>> origin/main
        return Ok(result);
    }

    /// <summary>Returns a single quote with its line items.</summary>
    /// <param name="id">Quote primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Quote DTO.</returns>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<QuoteDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var dto = await bus.InvokeAsync<QuoteDto>(new GetQuoteQuery(id), ct);
        return Ok(dto);
    }

<<<<<<< HEAD
    /// <summary>Creates a new quote.</summary>
=======
    /// <summary>Creates a new quote for a client.</summary>
>>>>>>> origin/main
    /// <param name="request">Quote creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new quote ID.</returns>
    [HttpPost]
<<<<<<< HEAD
    public async Task<ActionResult<int>> CreateAsync(
        [FromBody] CreateQuoteRequest request,
        CancellationToken ct)
    {
        var items = request.Items
            .Select(i => new QuoteItemRequest(i.Description, i.UnitPrice, i.Quantity))
            .ToList();

        var cmd = new CreateQuoteCommand(
            request.ClientId,
            request.Subject,
            request.ExpiryDate,
            request.Notes,
            items);
=======
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<int>> CreateAsync(
        [FromBody] CreateQuoteRequest request, CancellationToken ct)
    {
        var items = request.Items
            .Select(i => new QuoteItemRequest(i.Quantity, i.Description, i.UnitPrice, i.DiscountPercent, i.Taxed))
            .ToList();

        var cmd = new CreateQuoteCommand(
            request.ClientId, request.Subject, request.Stage, request.ValidUntil,
            request.ProposalText, request.CustomerNotes, request.AdminNotes, items);
>>>>>>> origin/main

        var id = await bus.InvokeAsync<int>(cmd, ct);
        return StatusCode(StatusCodes.Status201Created, id);
    }
<<<<<<< HEAD
=======

    /// <summary>Updates an existing quote's details and line items.</summary>
    /// <param name="id">Quote primary key.</param>
    /// <param name="request">Quote update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> UpdateAsync(
        int id, [FromBody] UpdateQuoteRequest request, CancellationToken ct)
    {
        var items = request.Items
            .Select(i => new UpdateQuoteItemEntry(
                i.Id, i.Quantity, i.Description, i.UnitPrice, i.DiscountPercent, i.Taxed, i.IsDeleted))
            .ToList();

        var cmd = new UpdateQuoteCommand(
            id, request.Subject, request.Stage, request.ValidUntil,
            request.ProposalText, request.CustomerNotes, request.AdminNotes, items);

        await bus.InvokeAsync(cmd, ct);
        return NoContent();
    }

    /// <summary>Duplicates a quote as a new draft.</summary>
    /// <param name="id">Quote primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new draft quote ID.</returns>
    [HttpPost("{id:int}/duplicate")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<int>> DuplicateAsync(int id, CancellationToken ct)
    {
        var newId = await bus.InvokeAsync<int>(new DuplicateQuoteCommand(id), ct);
        return StatusCode(StatusCodes.Status201Created, newId);
    }

    /// <summary>Converts a quote to a draft invoice.</summary>
    /// <param name="id">Quote primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new draft invoice ID.</returns>
    [HttpPost("{id:int}/convert-to-invoice")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<int>> ConvertToInvoiceAsync(int id, CancellationToken ct)
    {
        var invoiceId = await bus.InvokeAsync<int>(new ConvertQuoteToInvoiceCommand(id), ct);
        return StatusCode(StatusCodes.Status201Created, invoiceId);
    }

    /// <summary>Deletes a quote.</summary>
    /// <param name="id">Quote primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteQuoteCommand(id), ct);
        return NoContent();
    }
>>>>>>> origin/main
}
