namespace Innovayse.API.Billing;

using Innovayse.API.Billing.Extensions;
using Innovayse.API.Billing.Requests;
using Innovayse.API.Orders.Requests;
using Innovayse.Application.Billing.Commands.CompleteGatewayPayment;
using Innovayse.Application.Billing.Commands.CompleteMyGatewayPayment;
using Innovayse.Application.Billing.Commands.PayMyInvoice;
using Innovayse.Application.Billing.Commands.StartMyGatewayPayment;
using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Billing.Queries.GetMyInvoice;
using Innovayse.Application.Billing.Queries.GetMyInvoices;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Client portal endpoints for viewing and paying the authenticated client's invoices.
/// Requires Client role.
/// </summary>
/// <remarks>
/// No route here names a client and no action reads a claim. The rule that an invoice must belong
/// to the caller used to be written out in this file, four times, as a comparison against the
/// profile the endpoint had just fetched; it now lives in the client-facing handlers, which
/// resolve the caller themselves. An invoice belonging to somebody else and an invoice that does
/// not exist answer alike -- ids here are sequential, and telling those two apart is itself a way
/// of enumerating them.
/// </remarks>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/me/invoices")]
[Authorize(Roles = Roles.Client)]
public sealed class MyBillingController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns all invoices belonging to the authenticated client.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of invoice DTOs with line items.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<InvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<InvoiceDto>>> GetMineAsync(CancellationToken ct)
    {
        var invoices = await bus.InvokeAsync<IReadOnlyList<InvoiceDto>>(new GetMyInvoicesQuery(), ct);
        return Ok(invoices);
    }

    /// <summary>Returns a single invoice belonging to the authenticated client.</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The invoice DTO; 404 with <c>INVOICE_NOT_FOUND</c> when it is not the caller's.</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var invoice = await bus.InvokeAsync<InvoiceDto>(new GetMyInvoiceQuery(id), ct);
        return Ok(invoice);
    }

    /// <summary>Pays an invoice belonging to the authenticated client.</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="request">Payment request (currency).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success; 404 with <c>INVOICE_NOT_FOUND</c> when it is not the caller's.</returns>
    [HttpPost("{id:int}/pay")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PayAsync(int id, [FromBody] PayInvoiceRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new PayMyInvoiceCommand(id, request.Currency), ct);
        return NoContent();
    }

    /// <summary>Starts a hosted-gateway payment for the authenticated client's invoice.</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="request">The payment module and return URL.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with the redirect URL; 404 with <c>INVOICE_NOT_FOUND</c> when it is not the caller's.</returns>
    [HttpPost("{id:int}/gateway-payment/start")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartGatewayPaymentAsync(
        int id, [FromBody] StartGatewayPaymentRequest request, CancellationToken ct)
    {
        var redirectUrl = await bus.InvokeAsync<string>(
            new StartMyGatewayPaymentCommand(id, request.Module, request.ReturnUrl), ct);
        return Ok(new { redirectUrl });
    }

    /// <summary>Verifies a hosted-gateway payment for the authenticated client's invoice.</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with the payment state; 404 with <c>INVOICE_NOT_FOUND</c> when it is not the caller's.</returns>
    [HttpPost("{id:int}/gateway-payment/complete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteGatewayPaymentAsync(int id, CancellationToken ct)
    {
        var state = await bus.InvokeAsync<GatewayCompletionState>(new CompleteMyGatewayPaymentCommand(id), ct);
        return Ok(new { state = state.ToWireString() });
    }
}
