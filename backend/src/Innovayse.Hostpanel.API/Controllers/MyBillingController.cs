using Innovayse.Hostpanel.API.Authorization;
using Innovayse.Hostpanel.Application.Billing.Commands.CompleteMyGatewayPayment;
using Innovayse.Hostpanel.Application.Billing.Commands.PayMyInvoice;
using Innovayse.Hostpanel.Application.Billing.Commands.StartMyGatewayPayment;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetMyInvoice;
using Innovayse.Hostpanel.Application.Billing.Queries.GetMyInvoicePdf;
using Innovayse.Hostpanel.Application.Billing.Queries.GetMyInvoices;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;
/// <summary>
/// Client portal endpoints for viewing and paying the authenticated client's invoices.
/// </summary>
/// <remarks>
/// No route here names a client and no action reads a claim. The rule that an invoice must belong
/// to the caller lives in the client-facing handlers, which resolve the caller themselves. An
/// invoice belonging to somebody else and an invoice that does
/// not exist answer alike: ids here are sequential, and telling those two apart is itself a way
/// of enumerating them.
/// </remarks>
[Route("api/me/invoices")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class MyBillingController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public MyBillingController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns all invoices belonging to the authenticated client.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the caller's invoices; 401 without a session;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewPayInvoices</c>;
    /// 404 with <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller has no client record;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewPayInvoices)]
    [ProducesResponseType(typeof(IReadOnlyList<MyInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMineAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<MyInvoiceDto>>>(new GetMyInvoicesQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns a single invoice belonging to the authenticated client.</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the invoice; 401 without a session; 404 with <c>INVOICE_NOT_FOUND</c> when it is not the caller's;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewPayInvoices</c>;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewPayInvoices)]
    [ProducesResponseType(typeof(MyInvoiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<MyInvoiceDto>>(new GetMyInvoiceQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Downloads one of the authenticated client's published invoices as a PDF.</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with <c>invoice-{id}.pdf</c> as an attachment; 400 for an id that is not positive, or with
    /// <c>INVOICE_PDF_EXCEEDS_LIMIT</c> when the invoice has more lines than <c>InvoicePdf:MaxLines</c>;
    /// 401 without a session; 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewPayInvoices</c>;
    /// 404 with <c>INVOICE_NOT_FOUND</c> when it is not the caller's or is still a draft; 429 when the
    /// caller already has as many renders in flight as the concurrency policy allows; 503 with
    /// <c>INVOICE_PDF_RENDER_BUSY</c> and <c>Retry-After</c> when no render slot frees within
    /// <c>InvoicePdf:MaxRenderWaitSeconds</c>.
    /// </returns>
    [HttpGet("{id:int}/pdf")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewPayInvoices)]
    [EnableRateLimiting(RateLimitPolicies.Concurrent)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetPdfAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<FileDownloadDto>>(new GetMyInvoicePdfQuery(id), ct);
        return FileResult(outcome);
    }

    /// <summary>
    /// Pays an invoice belonging to the authenticated client with a payment method saved on their
    /// account, in the invoice's own currency.
    /// </summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="command">The saved payment method to charge; its invoice id comes from the route.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 once the money is taken; 401 without a session; 404 with <c>INVOICE_NOT_FOUND</c> when it is not the caller's;
    /// 400 with <c>PAYMENT_METHOD_NOT_OWNED</c>, <c>NO_SAVED_PAYMENT_METHODS</c> or <c>INVOICE_NOT_PAYABLE</c>;
    /// 409 with <c>PAYMENT_METHOD_UNAVAILABLE</c> when the deployment has no card gateway, or with
    /// <c>PAYMENT_INTENT_ALREADY_USED</c> when the gateway's transaction is already on the ledger;
    /// 502 with <c>GATEWAY_CHARGE_UPSTREAM_FAILED</c> when the gateway declines, or with
    /// <c>PAYMENT_METHOD_UPSTREAM_FAILED</c> when it refuses to list the saved methods or look up the charge;
    /// 503 with <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> when the gateway cannot be reached;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewPayInvoices</c>, or with
    /// <c>CLIENT_SUSPENDED</c>, <c>CLIENT_CLOSED</c> or <c>CLIENT_CARD_PAYMENT_DISABLED</c> when the account may not pay by card;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/pay")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewPayInvoices)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> PayAsync(int id, [FromBody] PayMyInvoiceCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { InvoiceId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Starts a hosted-gateway payment for the authenticated client's invoice.</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="command">The payment module and return URL; its invoice id comes from the route.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 OK with the redirect URL; 401 without a session; 404 with <c>INVOICE_NOT_FOUND</c> when it is not the caller's,
    /// or <c>CURRENCY_NOT_FOUND</c>; 400 with <c>INVOICE_NOT_PAYABLE</c>, <c>RETURN_URL_NOT_ALLOWED</c> or
    /// <c>GATEWAY_CURRENCY_MISMATCH</c>; 409 with <c>GATEWAY_SESSION_ALREADY_STARTED</c>; 503 with
    /// <c>PAYMENT_PLUGIN_NOT_CONFIGURED</c>, or with <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> when the
    /// gateway fails to register the payment; 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user
    /// lacks <c>ViewPayInvoices</c>, or with <c>CLIENT_SUSPENDED</c>, <c>CLIENT_CLOSED</c> or
    /// <c>CLIENT_CARD_PAYMENT_DISABLED</c> when the account may not pay;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/gateway-payment/start")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewPayInvoices)]
    [ProducesResponseType(typeof(RedirectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> StartGatewayPaymentAsync(
        int id, [FromBody] StartMyGatewayPaymentCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<RedirectDto>>(command with { InvoiceId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Verifies a hosted-gateway payment for the authenticated client's invoice.</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 OK with the payment state; 401 without a session; 404 with <c>INVOICE_NOT_FOUND</c> when it is not the caller's,
    /// or <c>GATEWAY_SESSION_NOT_FOUND</c>; 503 with <c>PAYMENT_PLUGIN_NOT_CONFIGURED</c>;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewPayInvoices</c>;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/gateway-payment/complete")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewPayInvoices)]
    [ProducesResponseType(typeof(GatewayCompletionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CompleteGatewayPaymentAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<GatewayCompletionDto>>(
            new CompleteMyGatewayPaymentCommand(id), ct);
        return HandleResult(outcome);
    }
}
