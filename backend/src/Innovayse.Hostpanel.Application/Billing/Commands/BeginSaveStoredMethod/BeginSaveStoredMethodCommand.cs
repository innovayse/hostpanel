namespace Innovayse.Hostpanel.Application.Billing.Commands.BeginSaveStoredMethod;

/// <summary>
/// Starts storing a payment method for the authenticated client at a payment gateway module, and records the client's
/// consent to have the stored method charged without them present. The consent is written before the payer reaches the
/// gateway step, together with the evidence of it: the wording version, the address and the browser.
/// </summary>
/// <remarks>
/// Carries no client id: whose account it is comes from the credential, inside the handler. The address and browser
/// are the request's own, set by the controller from the connection and the headers; whatever a body says for them
/// is overwritten, so a client cannot write its own evidence.
/// </remarks>
/// <param name="GatewayModuleId">The payment plugin id that stores the method (for example "bank-gateway").</param>
/// <param name="ConsentAccepted">Whether the client ticked the consent box. The validator requires <see langword="true"/>.</param>
/// <param name="ReturnUrl">
/// The absolute address the payer comes back to after a redirect gateway step; its origin must be one the deployment
/// allows. An embedded plugin ignores it.
/// </param>
/// <param name="ConsentTextVersion">
/// The version of the consent wording the payer ticked, as the portal showed it; kept as evidence. The wire value of a
/// published version (<see cref="Innovayse.Hostpanel.Domain.Billing.Enums.ConsentTextVersion"/>).
/// </param>
/// <param name="IpAddress">The address of the request, set by the controller; not read from the body.</param>
/// <param name="UserAgent">The browser identification of the request, set by the controller; not read from the body.</param>
public sealed record BeginSaveStoredMethodCommand(
    string GatewayModuleId,
    bool ConsentAccepted,
    string ReturnUrl,
    string ConsentTextVersion,
    string? IpAddress = null,
    string? UserAgent = null);
