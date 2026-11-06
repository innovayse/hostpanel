using Innovayse.Hostpanel.Application.Billing.Commands.CreateTransaction;
using Innovayse.Hostpanel.Application.Billing.Commands.DeleteTransaction;
using Innovayse.Hostpanel.Application.Billing.Commands.UpdateTransaction;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetTransactionById;
using Innovayse.Hostpanel.Application.Billing.Queries.ListTransactions;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>Admin endpoints for a client's ledger of financial transactions.</summary>
[Route("api/transactions")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class TransactionsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public TransactionsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns a paginated list of all transactions.</summary>
    /// <param name="query">The page and page size to list by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the transactions; 400 when a value is refused; 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(TransactionsResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync([FromQuery] ListTransactionsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<TransactionsResultDto>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns a paginated list of transactions for a specific client.</summary>
    /// <param name="clientId">The client's primary key; wins over any client id in the query.</param>
    /// <param name="query">The page and page size to list by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the client's transactions; 400 when a value is refused;
    /// 401 when the caller is not signed in; 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("client/{clientId:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(TransactionsResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListByClientAsync(
        int clientId, [FromQuery] ListTransactionsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<TransactionsResultDto>>(
            query with { ClientId = clientId }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns a single transaction by ID.</summary>
    /// <param name="id">Transaction primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the transaction; 400 when a value is refused; 404 when the id names no transaction;
    /// 401 when the caller is not signed in; 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(TransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByIdAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<TransactionDto>>(new GetTransactionByIdQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Creates a new transaction, optionally adjusting the client's credit balance.</summary>
    /// <param name="command">The transaction to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new transaction's id; 400 when a value is refused; 422 when no money moves; 404 when the client does not exist;
    /// 401 when the caller is not signed in; 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateTransactionCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/transactions/{id}", id));
    }

    /// <summary>Updates an existing transaction's editable fields.</summary>
    /// <param name="id">Transaction primary key; wins over any id in the body.</param>
    /// <param name="command">The fields to write.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 when a value is refused; 422 when no money moves; 404 when the id names no transaction;
    /// 409 with <c>TRANSACTION_CREDIT_LOCKED</c> when the entry is part of the credit history; 401 when the caller is not signed in; 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAsync(
        int id, [FromBody] UpdateTransactionCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { Id = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Deletes a transaction and reverses any credit adjustments it made.</summary>
    /// <param name="id">Transaction primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 when a value is refused or the credit the transaction added is already
    /// spent; 404 when the id names no transaction or its client; 409 with <c>TRANSACTION_CREDIT_LOCKED</c> when the entry is
    /// part of the credit history; 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new DeleteTransactionCommand(id), ct);
        return HandleResult(outcome);
    }
}
