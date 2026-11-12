using Innovayse.Hostpanel.API.Authorization;
using Innovayse.Hostpanel.Application.Billing.Commands.ApplyMyInvoiceCredit;
using Innovayse.Hostpanel.Application.Billing.Commands.CompleteMyGatewayPayment;
using Innovayse.Hostpanel.Application.Billing.Commands.SelectMyBankTransfer;
using Innovayse.Hostpanel.Application.Billing.Commands.StartMyGatewayPayment;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetMyBankTransfer;
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
/// Every action behind the client portal policy may also answer 503 with <c>IDENTITY_PLUGIN_UNAVAILABLE</c> (and <c>Retry-After</c>)
/// when the enabled identity plugin cannot say which account the caller acts for; the convention
/// <c>IdentityPluginUnavailableConvention</c> declares it once in the generated document.
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

    /// <summary>Starts a hosted-gateway payment for the authenticated client's invoice.</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="command">
    /// The payment module and return URL, and optionally the choice to keep the payment method with the consent wording
    /// version; its invoice id comes from the route, and the address and browser kept as consent evidence are the
    /// request's own, set here over whatever the body says.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 OK with the redirect URL; 401 without a session; 404 with <c>INVOICE_NOT_FOUND</c> when it is not the caller's,
    /// or <c>CURRENCY_NOT_FOUND</c>; 400 with <c>VALIDATION_ERROR</c> (a method to keep without the consent wording
    /// version), <c>INVOICE_NOT_PAYABLE</c>, <c>RETURN_URL_NOT_ALLOWED</c> or
    /// <c>GATEWAY_CURRENCY_MISMATCH</c>; 409 with <c>GATEWAY_SESSION_ALREADY_STARTED</c>, <c>INVOICE_STORED_CHARGE_UNRESOLVED</c>,
    /// <c>INVOICE_STORED_CHARGE_HELD</c> or <c>STORED_CREDENTIALS_NOT_SUPPORTED</c> (a method to keep at a module that cannot keep one); 503 with
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
        // The consent evidence is the connection's and the headers', never the body's: the overlay wins over any value sent.
        var outcome = await _bus.InvokeAsync<Result<RedirectDto>>(
            command with { InvoiceId = id, IpAddress = RequestIpAddress, UserAgent = RequestUserAgent },
            ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Chooses manual bank transfer for the authenticated client's invoice: the invoice waits for staff to confirm the
    /// money, and the answer carries the bank details and the reference to write on the transfer.
    /// </summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the bank details and the reference (also when the transfer was already chosen); 401 without a session;
    /// 404 with <c>INVOICE_NOT_FOUND</c> when it is not the caller's; 400 with <c>INVOICE_NOT_PAYABLE</c> when the invoice
    /// is not open for payment; 409 with <c>BANK_TRANSFER_NOT_AVAILABLE</c> when the admin has it switched off,
    /// <c>GATEWAY_SESSION_ALREADY_STARTED</c> while a hosted payment is still open, or <c>INVOICE_STORED_CHARGE_UNRESOLVED</c>
    /// or <c>INVOICE_STORED_CHARGE_HELD</c> while a stored card charge blocks it; 403 with <c>CLIENT_PERMISSION_DENIED</c>
    /// when the user lacks <c>ViewPayInvoices</c>, or with <c>CLIENT_SUSPENDED</c> or <c>CLIENT_CLOSED</c> when the account
    /// may not pay; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/bank-transfer")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewPayInvoices)]
    [ProducesResponseType(typeof(BankTransferDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SelectBankTransferAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<BankTransferDetailsDto>>(new SelectMyBankTransferCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns the bank details and the reference of the manual bank transfer waiting on the authenticated client's invoice.</summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the bank details and the reference; 401 without a session; 404 with <c>INVOICE_NOT_FOUND</c> when it is not
    /// the caller's; 409 with <c>INVOICE_BANK_TRANSFER_NOT_PENDING</c> when no transfer waits on it; 403 with
    /// <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewPayInvoices</c>; 400 when the id is not positive;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}/bank-transfer")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewPayInvoices)]
    [ProducesResponseType(typeof(BankTransferDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetBankTransferAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<BankTransferDetailsDto>>(new GetMyBankTransferQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Pays part or all of the authenticated client's invoice from their credit balance.</summary>
    /// <param name="id">Invoice primary key; wins over any invoice id in the body.</param>
    /// <param name="command">The credit amount to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when applied (the invoice turns Paid when the credit covers all that was owed); 401 without a session; 404 with
    /// <c>INVOICE_NOT_FOUND</c> when it is not the caller's; 400 with <c>CLIENT_CREDIT_INSUFFICIENT</c> when the balance is
    /// short, <c>INVOICE_CREDIT_EXCEEDS_DUE</c> when more than is owed, <c>INVOICE_NOT_EDITABLE</c> when the invoice no longer
    /// takes credit, <c>INVOICE_TOP_UP_NO_CREDIT</c> for a top-up invoice, or when the amount fails validation; 403 with
    /// <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewPayInvoices</c>, or with <c>CLIENT_SUSPENDED</c> or
    /// <c>CLIENT_CLOSED</c> when the account may not pay; 409 when the invoice or the balance changed under the request;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/credit")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewPayInvoices)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApplyCreditAsync(
        int id, [FromBody] ApplyMyInvoiceCreditCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { InvoiceId = id }, ct);
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
