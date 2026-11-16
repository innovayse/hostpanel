using Innovayse.Hostpanel.Application.Billing.Commands.ConvertQuoteToInvoice;
using Innovayse.Hostpanel.Application.Billing.Commands.CreateQuote;
using Innovayse.Hostpanel.Application.Billing.Commands.DeleteQuote;
using Innovayse.Hostpanel.Application.Billing.Commands.DuplicateQuote;
using Innovayse.Hostpanel.Application.Billing.Commands.UpdateQuote;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetQuote;
using Innovayse.Hostpanel.Application.Billing.Queries.ListClientQuotes;
using Innovayse.Hostpanel.Application.Billing.Queries.ListQuotes;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>Admin endpoints for managing quotes.</summary>
[Route("api/billing/quotes")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class QuotesController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public QuotesController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns a paginated list of all quotes.</summary>
    /// <param name="query">The paging window, the optional subject or id search and the optional client, stage and validity filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the page; 400 when the request fails validation; 401 without a session; 403 for a caller who is not an admin; 429 when the rate limit is hit.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<QuoteListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync([FromQuery] ListQuotesQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<QuoteListItemDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns a paginated list of quotes for a specific client.</summary>
    /// <param name="clientId">The client's primary key; wins over any client id in the query.</param>
    /// <param name="query">The paging window, the optional subject or id search and the optional stage and validity filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the page; 400 when the request fails validation; 401 without a session; 403 for a caller who is not an admin; 429 when the rate limit is hit.</returns>
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
    /// 200 with the quote; 400 when the id is invalid; 404 when the id names no quote; 401 without a session; 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(QuoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByIdAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<QuoteDto>>(new GetQuoteQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Creates a new quote.</summary>
    /// <param name="command">The quote to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the new quote ID; 400 when the quote fails validation; 404 when the client does not exist; 401 without a session; 403 for a caller who is not an admin; 429 when the rate limit is hit.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateQuoteCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/billing/quotes/{id}", id));
    }

    /// <summary>Updates an existing quote's details and line items.</summary>
    /// <param name="id">Quote primary key; wins over any quote id in the body.</param>
    /// <param name="command">The quote data to write.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when written; 400 when the request fails validation; 404 when the id names no quote; 401 without a session; 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateQuoteCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { QuoteId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Duplicates an existing quote as a new draft.</summary>
    /// <param name="id">Quote primary key to duplicate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new draft quote ID; 400 when the request fails validation; 404 when the id names no quote; 401 without a session;
    /// 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/duplicate")]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DuplicateAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(new DuplicateQuoteCommand(id), ct);
        return HandleResult(outcome, newId => CreatedResult($"/api/billing/quotes/{newId}", newId));
    }

    /// <summary>Converts a quote into a draft invoice.</summary>
    /// <param name="id">Quote primary key to convert.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new draft invoice ID; 400 when the request fails validation; 404 when the id names no quote; 401 without a session;
    /// 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/convert-to-invoice")]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConvertToInvoiceAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(new ConvertQuoteToInvoiceCommand(id), ct);
        return HandleResult(outcome, invoiceId => CreatedResult($"/api/billing/{invoiceId}", invoiceId));
    }

    /// <summary>Permanently deletes a quote.</summary>
    /// <param name="id">Quote primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when deleted; 400 when the request fails validation; 404 when the id names no quote; 401 without a session; 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new DeleteQuoteCommand(id), ct);
        return HandleResult(outcome);
    }
}
