using Innovayse.Hostpanel.Application.Billing.Commands.ConvertQuoteToInvoice;
using Innovayse.Hostpanel.Application.Billing.Commands.CreateQuote;
using Innovayse.Hostpanel.Application.Billing.Commands.DeleteQuote;
using Innovayse.Hostpanel.Application.Billing.Commands.DuplicateQuote;
using Innovayse.Hostpanel.Application.Billing.Commands.UpdateQuote;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetQuote;
using Innovayse.Hostpanel.Application.Billing.Queries.ListClientQuotes;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Client-scoped endpoints for managing quotes, used by the client profile views. Route:
/// <c>api/quotes</c>; separate from <see cref="QuotesController"/>'s <c>api/billing/quotes</c>.
/// </summary>
[Route("api/quotes")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class ClientQuotesController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public ClientQuotesController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns a paginated list of quotes for a specific client.</summary>
    /// <param name="clientId">The client's primary key; wins over any client id in the query.</param>
    /// <param name="query">The paging window, the optional subject or id search and the optional stage and validity filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the page; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not staff;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("client/{clientId:int}")]
    [ProducesResponseType(typeof(PaginatedList<QuoteListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListByClientAsync(
        int clientId, [FromQuery] ListClientQuotesQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<QuoteListItemDto>>>(
            query with { ClientId = clientId }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns a single quote with its line items.</summary>
    /// <param name="id">Quote primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the quote; 404 with <c>QUOTE_NOT_FOUND</c>; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not staff;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(QuoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<QuoteDto>>(new GetQuoteQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Creates a new quote for a client.</summary>
    /// <param name="command">The quote to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new quote ID; 400 when the body, query or route fails validation; 404 when the client does not exist; 401 without a session; 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateQuoteCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/quotes/{id}", id));
    }

    /// <summary>Updates an existing quote's details and line items.</summary>
    /// <param name="id">Quote primary key; wins over any quote id in the body.</param>
    /// <param name="command">The quote data to write.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when written; 404 with <c>QUOTE_NOT_FOUND</c>; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateQuoteCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { QuoteId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Duplicates a quote as a new draft.</summary>
    /// <param name="id">Quote primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new draft quote ID; 404 with <c>QUOTE_NOT_FOUND</c>; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/duplicate")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DuplicateAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(new DuplicateQuoteCommand(id), ct);
        return HandleResult(outcome, newId => CreatedResult($"/api/quotes/{newId}", newId));
    }

    /// <summary>Converts a quote to a draft invoice.</summary>
    /// <param name="id">Quote primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new draft invoice ID; 404 with <c>QUOTE_NOT_FOUND</c>; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/convert-to-invoice")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConvertToInvoiceAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(new ConvertQuoteToInvoiceCommand(id), ct);
        return HandleResult(outcome, invoiceId => CreatedResult($"/api/billing/{invoiceId}", invoiceId));
    }

    /// <summary>Deletes a quote.</summary>
    /// <param name="id">Quote primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when deleted; 404 with <c>QUOTE_NOT_FOUND</c>; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new DeleteQuoteCommand(id), ct);
        return HandleResult(outcome);
    }
}
