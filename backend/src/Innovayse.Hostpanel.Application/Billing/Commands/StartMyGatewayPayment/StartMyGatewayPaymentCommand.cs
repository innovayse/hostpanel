namespace Innovayse.Hostpanel.Application.Billing.Commands.StartMyGatewayPayment;

/// <summary>Command for a client to start a hosted-gateway payment on one of their own invoices.</summary>
/// <remarks>
/// Carries no client id. Which account the invoice must belong to is resolved inside the handler
/// from the credential. The order checkout flow, which starts a gateway payment for an invoice it
/// has just created, dispatches <c>StartGatewayPaymentCommand</c> directly.
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
/// <paramref name="SavePaymentMethod"/> is set, and then the wire value of a published version
/// (<see cref="Innovayse.Hostpanel.Domain.Billing.Enums.ConsentTextVersion"/>); ignored otherwise.
/// </param>
/// <param name="IpAddress">The address of the request, set by the controller; not read from the body.</param>
/// <param name="UserAgent">The browser identification of the request, set by the controller; not read from the body.</param>
public sealed record StartMyGatewayPaymentCommand(
    int InvoiceId,
    string Module,
    string ReturnUrl,
    bool SavePaymentMethod = false,
    string? ConsentTextVersion = null,
    string? IpAddress = null,
    string? UserAgent = null);
