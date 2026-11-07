using Innovayse.Hostpanel.API.Authorization;
using Innovayse.Hostpanel.Application.Billing.Commands.CreateMyCreditTopUp;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetMyCredit;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Client portal endpoints for the authenticated client's own credit: the balance, its history, and adding funds.
/// Every action behind the client portal policy may also answer 503 with <c>IDENTITY_PLUGIN_UNAVAILABLE</c> (and <c>Retry-After</c>)
/// when the enabled identity plugin cannot say which account the caller acts for; the convention
/// <c>IdentityPluginUnavailableConvention</c> declares it once in the generated document.
/// </summary>
/// <remarks>
/// No route here names a client and no action reads a claim: the account is resolved inside the handlers from the credential.
/// Adding funds raises a top-up invoice, which the client then pays through the invoice routes (<c>MyBillingController</c>).
/// </remarks>
[Route("api/me/credit")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class MyCreditController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public MyCreditController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns the authenticated client's credit: the balance, the top-up limits and a page of the history.</summary>
    /// <param name="query">The page and page size of the history.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the credit; 401 without a session; 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks
    /// <c>ViewPayInvoices</c>; 404 with <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller has no client record;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewPayInvoices)]
    [ProducesResponseType(typeof(MyCreditDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMineAsync([FromQuery] GetMyCreditQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<MyCreditDto>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Adds funds to the authenticated client's credit balance by raising a top-up invoice for the amount, which the client then
    /// pays like any invoice (a bank transfer or an installed gateway). The balance rises when the invoice is paid.
    /// </summary>
    /// <param name="command">The amount to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the id of the new invoice; 400 with <c>CREDIT_TOP_UP_AMOUNT_OUT_OF_RANGE</c> when the amount is below the
    /// minimum or above the maximum, or when it fails validation; 409 with <c>CREDIT_TOP_UP_TOO_MANY_OPEN</c> when the client
    /// already has as many unpaid top-up invoices as the admin allows; 401 without a session; 403 with <c>CLIENT_PERMISSION_DENIED</c>
    /// when the user lacks <c>ViewPayInvoices</c>, or with <c>CLIENT_SUSPENDED</c> or <c>CLIENT_CLOSED</c> when the account may
    /// not transact; 404 with <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller has no client record; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("top-ups")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewPayInvoices)]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTopUpAsync([FromBody] CreateMyCreditTopUpCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/me/invoices/{id}", id));
    }
}
