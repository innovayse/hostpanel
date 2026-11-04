using Innovayse.SDK.Plugins.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>A started "save a payment method" session the browser completes at the gateway.</summary>
/// <param name="GatewayModuleId">The payment gateway module the method is being saved with.</param>
/// <param name="SessionId">The id the portal hands back when the payer is done.</param>
/// <param name="Mode">How the payer completes the session: with the plugin's own card form and <paramref name="ClientToken"/>, or at <paramref name="RedirectUrl"/>.</param>
/// <param name="ClientToken">The token the gateway's own card form needs, set for an embedded session.</param>
/// <param name="RedirectUrl">The gateway page the payer is sent to, set for a redirect session.</param>
public sealed record SavedMethodSessionDto(
    string GatewayModuleId,
    string SessionId,
    SaveMethodMode Mode,
    string? ClientToken,
    string? RedirectUrl);
