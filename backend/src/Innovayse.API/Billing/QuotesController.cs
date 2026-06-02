namespace Innovayse.API.Billing;

using Innovayse.API.Billing.Requests;
using Innovayse.Application.Billing.Commands.CreateQuote;
using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Billing.Queries.GetQuote;
using Innovayse.Application.Billing.Queries.ListClientQuotes;
using Innovayse.Application.Billing.Queries.ListQuotes;
using Innovayse.Application.Common;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing quotes.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/billing/quotes")]
[Authorize(Roles = Roles.Admin)]
public sealed class QuotesController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns a paginated list of all quotes.</summary>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page (default 20, max 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated quote list.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResult<QuoteListItemDto>>> GetAllAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<PagedResult<QuoteListItemDto>>(
            new ListQuotesQuery(page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Returns a paginated list of quotes for a specific client.</summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page (default 20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated quote list for the client.</returns>
    [HttpGet("client/{clientId:int}")]
    public async Task<ActionResult<PagedResult<QuoteListItemDto>>> GetByClientAsync(
        int clientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<PagedResult<QuoteListItemDto>>(
            new ListClientQuotesQuery(clientId, page, pageSize), ct);
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

    /// <summary>Creates a new quote.</summary>
    /// <param name="request">Quote creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new quote ID.</returns>
    [HttpPost]
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

        var id = await bus.InvokeAsync<int>(cmd, ct);
        return StatusCode(StatusCodes.Status201Created, id);
    }
}
