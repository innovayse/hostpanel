namespace Innovayse.Hostpanel.Infrastructure.Integrations.MaxMind;

/// <summary>The <c>traits</c> of an IP address in a minFraud Insights answer.</summary>
/// <param name="IsAnonymous">Whether the address is any kind of anonymiser.</param>
/// <param name="IsAnonymousVpn">Whether the address belongs to a VPN.</param>
/// <param name="IsHostingProvider">Whether the address belongs to a hosting provider.</param>
/// <param name="IsPublicProxy">Whether the address is a public proxy.</param>
/// <param name="IsResidentialProxy">Whether the address is a residential proxy.</param>
/// <param name="IsTorExitNode">Whether the address is a Tor exit node.</param>
internal sealed record MaxMindIpTraitsWire(
    bool? IsAnonymous,
    bool? IsAnonymousVpn,
    bool? IsHostingProvider,
    bool? IsPublicProxy,
    bool? IsResidentialProxy,
    bool? IsTorExitNode);
