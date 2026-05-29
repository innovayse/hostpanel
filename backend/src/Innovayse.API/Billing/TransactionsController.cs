namespace Innovayse.API.Billing;

using Innovayse.API.Billing.Requests;
<<<<<<< HEAD
using Innovayse.Application.Billing.Commands.CreateTransaction;
using Innovayse.Application.Billing.Commands.DeleteTransaction;
using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Billing.Queries.ListTransactions;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
=======
using Innovayse.Application.Billing.Commands.CreateClientTransaction;
using Innovayse.Application.Billing.Commands.DeleteClientTransaction;
using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Billing.Queries.ListClientTransactions;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
>>>>>>> origin/main
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
<<<<<<< HEAD
/// Admin endpoints for managing client-level financial transactions.
=======
/// Admin and Reseller endpoints for managing client transactions.
>>>>>>> origin/main
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/transactions")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Reseller}")]
public sealed class TransactionsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns a paginated list of transactions for a specific client with financial summary.</summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="page">1-based page number (default 1).</param>
<<<<<<< HEAD
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
=======
    /// <param name="pageSize">Items per page (default 20, max 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Transactions result with summary totals.</returns>
    [HttpGet("client/{clientId:int}")]
    public async Task<ActionResult<ClientTransactionsResultDto>> GetByClientAsync(
        int clientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<ClientTransactionsResultDto>(
            new ListClientTransactionsQuery(clientId, page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Creates a new client transaction.</summary>
>>>>>>> origin/main
    /// <param name="request">Transaction creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new transaction ID.</returns>
    [HttpPost]
<<<<<<< HEAD
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<int>> CreateAsync(
        [FromBody] CreateTransactionRequest request,
        CancellationToken ct = default)
    {
        var cmd = new CreateTransactionCommand(
=======
    public async Task<ActionResult<int>> CreateAsync(
        [FromBody] CreateClientTransactionRequest request, CancellationToken ct)
    {
        var cmd = new CreateClientTransactionCommand(
>>>>>>> origin/main
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
<<<<<<< HEAD
        return StatusCode(StatusCodes.Status201Created, id);
    }

    /// <summary>Deletes a transaction and reverses any credit adjustments.</summary>
=======
        return Created($"/api/transactions/client/{request.ClientId}", id);
    }

    /// <summary>Deletes a client transaction and reverses any credit adjustments.</summary>
>>>>>>> origin/main
    /// <param name="id">Transaction primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpDelete("{id:int}")]
<<<<<<< HEAD
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteTransactionCommand(id), ct);
=======
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteClientTransactionCommand(id), ct);
>>>>>>> origin/main
        return NoContent();
    }
}
