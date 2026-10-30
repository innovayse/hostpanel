using Innovayse.Hostpanel.Application.Billing.Commands.AddInvoicePayment;
using Innovayse.Hostpanel.Application.Billing.Commands.ApplyInvoiceCredit;
using Innovayse.Hostpanel.Application.Billing.Commands.BulkInvoiceAction;
using Innovayse.Hostpanel.Application.Billing.Commands.RefundInvoicePayment;
using Innovayse.Hostpanel.Application.Billing.Commands.RemoveInvoiceCredit;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Admin endpoints for recording invoice payments, applying credits, refunds and bulk lifecycle
/// actions. The invoice lifecycle itself lives in the neighbouring <c>InvoicesController</c>.
/// </summary>
[Route("api/billing")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class InvoicePaymentsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public InvoicePaymentsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Records a manual payment against an invoice.</summary>
    /// <param name="id">Invoice primary key; wins over any invoice id in the body.</param>
    /// <param name="command">The payment details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when recorded; 404 when the id names no invoice; 401 without a session; 403 for a caller who is not an admin;
    /// 400 when the body, query or route fails validation; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/payment")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddPaymentAsync(
        int id, [FromBody] AddInvoicePaymentCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { InvoiceId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Applies a credit to an invoice.</summary>
    /// <param name="id">Invoice primary key; wins over any invoice id in the body.</param>
    /// <param name="command">The credit amount to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when applied; 404 when the id names no invoice; 401 without a session; 403 for a caller who is not an admin;
    /// 400 when the body, query or route fails validation; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/credit")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApplyCreditAsync(
        int id, [FromBody] ApplyInvoiceCreditCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { InvoiceId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Removes a specific credit amount from an invoice.</summary>
    /// <param name="id">Invoice primary key; wins over any invoice id in the body.</param>
    /// <param name="command">The credit amount to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when removed; 404 when the id names no invoice; 401 without a session; 403 for a caller who is not an admin;
    /// 400 when the body, query or route fails validation; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/credit/remove")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveCreditAsync(
        int id, [FromBody] RemoveInvoiceCreditCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { InvoiceId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Refunds all or part of a paid invoice. A refund that leaves part of the money turns the invoice
    /// PartiallyRefunded, which can be refunded again up to what is left; one that reaches the whole
    /// amount paid turns it Refunded.
    /// </summary>
    /// <param name="id">Invoice primary key; wins over any invoice id in the body.</param>
    /// <param name="command">The refund details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when refunded; 400 when the invoice is neither Paid nor PartiallyRefunded, the amount exceeds what is still
    /// refundable, or a gateway refund is asked for money no gateway took; 404 when the id names no
    /// invoice, or a CreditBalance refund names a client that no longer exists; 409 with
    /// <c>PAYMENT_METHOD_UNAVAILABLE</c> when the card gateway that took the money is no longer
    /// configured; 502 with <c>CARD_REFUND_REFUSED</c> when the card gateway refuses the refund; 503
    /// when the invoice's hosted-gateway plugin is not configured; 401 without a session; 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/refund")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RefundAsync(
        int id, [FromBody] RefundInvoicePaymentCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { InvoiceId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Performs a bulk action on multiple invoices.</summary>
    /// <param name="command">The invoice IDs and the action name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the count of invoices affected; 400 when the action name is not recognised; 404 when
    /// a cancelled invoice's credit has no client to go back to; 401 without a session; 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("bulk")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BulkActionAsync([FromBody] BulkInvoiceActionCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome);
    }
}
