using Innovayse.Hostpanel.Application.Billing.Common.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>Where the payer's browser is sent to pay on a hosted gateway page, and how.</summary>
/// <param name="RedirectUrl">
/// The gateway page address: the page a redirect opens, the page of a browser step, or for a form post the address the form
/// is posted to.
/// </param>
/// <param name="Kind">How the browser is taken to the page.</param>
/// <param name="FormPost">The form to post; set exactly when <paramref name="Kind"/> is <see cref="GatewaySessionKind.FormPost"/>.</param>
public sealed record RedirectDto(string RedirectUrl, GatewaySessionKind Kind, PaymentFormPostDto? FormPost);
