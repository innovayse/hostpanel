namespace Innovayse.Hostpanel.Application.Billing.Commands.StartGatewayPayment;

/// <summary>
/// Starts a hosted-gateway payment for an invoice: registers the payment at the
/// gateway and records the session on the invoice.
/// </summary>
/// <remarks>
/// With <paramref name="SavePaymentMethod"/> the payment also asks the gateway to keep the payer's method, which is
/// only for the invoice's own client and records that client's consent to later charges together with the evidence of it.
/// The address and browser are the request's own, set by the controller from the connection and the headers; whatever a
/// body says for them is overwritten, so a client cannot write its own evidence.
/// </remarks>
/// <param name="InvoiceId">The invoice to pay.</param>
/// <param name="Module">The payment plugin id (e.g. "bank-gateway").</param>
/// <param name="ReturnUrl">Absolute URL the gateway redirects the payer back to.</param>
/// <param name="SavePaymentMethod">
/// Whether the payer asked to keep the payment method they pay with, so the host can charge it later. Needs a gateway
/// module that offers stored credentials; the consent evidence below is recorded with the payment.
/// </param>
/// <param name="ConsentTextVersion">
/// The version of the consent wording the payer ticked, as the portal showed it; kept as evidence. Required when
/// <paramref name="SavePaymentMethod"/> is set, ignored otherwise.
/// </param>
/// <param name="IpAddress">The address of the request, set by the controller; not read from the body.</param>
/// <param name="UserAgent">The browser identification of the request, set by the controller; not read from the body.</param>
public sealed record StartGatewayPaymentCommand(
    int InvoiceId,
    string Module,
    string ReturnUrl,
    bool SavePaymentMethod = false,
    string? ConsentTextVersion = null,
    string? IpAddress = null,
    string? UserAgent = null);
