namespace Innovayse.API.Billing;

using Innovayse.API.Billing.Requests;
using Innovayse.Application.Billing.Commands.CreateTransaction;
using Innovayse.Application.Billing.Commands.UpdateTransaction;
using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Billing.Queries.GetTransaction;
using Innovayse.Application.Billing.Queries.ListTransactions;
using Innovayse.Application.Common;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for viewing transaction records.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/billing/transactions")]
[Authorize(Roles = Roles.Admin)]
public sealed class TransactionsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns a paginated list of all transactions.</summary>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page (default 20, max 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated transaction list.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResult<TransactionDto>>> GetAllAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<PagedResult<TransactionDto>>(
            new ListTransactionsQuery(page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Gets a transaction by ID.</summary>
    /// <param name="id">Transaction ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The transaction details.</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<TransactionDto>> GetByIdAsync(
        int id,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<TransactionDto>(
            new GetTransactionByIdQuery(id), ct);
        return Ok(result);
    }

    /// <summary>Creates a new transaction record.</summary>
    /// <param name="request">Transaction creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new transaction ID.</returns>
    [HttpPost]
    public async Task<ActionResult<int>> CreateAsync(
        [FromBody] CreateTransactionRequest request,
        CancellationToken ct = default)
    {
        var cmd = new CreateTransactionCommand(
            request.ClientId,
            request.Type,
            request.Amount,
            request.Fees,
            request.Currency,
            request.Description,
            request.Gateway,
            request.TransactionId);

        var transactionId = await bus.InvokeAsync<int>(cmd, ct);
        return StatusCode(StatusCodes.Status201Created, transactionId);
    }

    /// <summary>Updates an existing transaction record.</summary>
    /// <param name="id">Transaction ID.</param>
    /// <param name="request">Transaction update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAsync(
        int id,
        [FromBody] CreateTransactionRequest request,
        CancellationToken ct = default)
    {
        var cmd = new UpdateTransactionCommand(
            id,
            request.ClientId,
            request.Type,
            request.Amount,
            request.Fees,
            request.Currency,
            request.Description,
            request.Gateway,
            request.TransactionId);

        await bus.InvokeAsync(cmd, ct);
        return NoContent();
    }
}
