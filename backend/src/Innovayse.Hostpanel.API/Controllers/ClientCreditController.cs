using Innovayse.Hostpanel.Application.Billing.Commands.AdjustClientCredit;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetClientCredit;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>Admin endpoints for one client's credit: the balance with its history, and adding or taking credit by hand.</summary>
[Route("api/clients/{clientId:int}/credit")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class ClientCreditController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public ClientCreditController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns a client's credit: the balance and a page of the history, newest first.</summary>
    /// <param name="clientId">The client's primary key; wins over any client id in the query.</param>
    /// <param name="query">The page and page size of the history.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the credit; 400 when a value is refused; 401 when the caller is not signed in; 403 when the caller's role is
    /// not allowed; 404 with <c>CLIENT_NOT_FOUND</c> when the id names no client; 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(ClientCreditDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(int clientId, [FromQuery] GetClientCreditQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<ClientCreditDto>>(query with { ClientId = clientId }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Adds credit to a client's balance or takes it off, with a reason; the client is told the new balance.</summary>
    /// <param name="clientId">The client's primary key; wins over any client id in the body.</param>
    /// <param name="command">The direction, the amount and the reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when adjusted; 400 when a value is refused, or with <c>CLIENT_CREDIT_INSUFFICIENT</c> when the deduction is more than
    /// the balance holds; 401 when the caller is not signed in; 403 when the caller's role is not allowed; 404 with
    /// <c>CLIENT_NOT_FOUND</c> when the id names no client; 409 when the balance changed under the adjustment; 429 when the
    /// rate limit is hit.
    /// </returns>
    [HttpPost("adjustments")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AdjustAsync(
        int clientId, [FromBody] AdjustClientCreditCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { ClientId = clientId }, ct);
        return HandleResult(outcome);
    }
}
