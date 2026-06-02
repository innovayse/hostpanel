namespace Innovayse.API.Billing;

using Innovayse.API.Billing.Requests;
using Innovayse.Application.Billing.Commands.CreateTransaction;
using Innovayse.Application.Billing.Commands.DeleteTransaction;
using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Billing.Queries.ListTransactions;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing client-level financial transactions.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/transactions")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Reseller}")]
public sealed class TransactionsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns a paginated list of all transactions.</summary>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page (default 25, max 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated transactions with summary totals.</returns>
    [HttpGet]
    public async Task<ActionResult<TransactionsResultDto>> GetAllAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<TransactionsResultDto>(
            new ListTransactionsQuery(null, page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Returns a paginated list of transactions for a specific client.</summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page (default 25, max 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated client transactions with summary totals.</returns>
    [HttpGet("client/{clientId:int}")]
    public async Task<ActionResult<TransactionsResultDto>> GetByClientAsync(
        int clientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<TransactionsResultDto>(
            new ListTransactionsQuery(clientId, page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Creates a new transaction.</summary>
    /// <param name="request">Transaction creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new transaction ID.</returns>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<int>> CreateAsync(
        [FromBody] CreateTransactionRequest request,
        CancellationToken ct = default)
    {
        var cmd = new CreateTransactionCommand(
            request.ClientId,
            request.Date,
            request.Description,
            request.TransactionId,
            request.InvoiceId,
            request.PaymentMethod,
            request.AmountIn,
            request.AmountOut,
            request.Fees,
            request.AddToCredit);

        var id = await bus.InvokeAsync<int>(cmd, ct);
        return StatusCode(StatusCodes.Status201Created, id);
    }

    /// <summary>Deletes a transaction and reverses any credit adjustments.</summary>
    /// <param name="id">Transaction primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteTransactionCommand(id), ct);
        return NoContent();
    }
}
