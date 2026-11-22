namespace Innovayse.Hostpanel.Application.Domains.Common;

/// <summary>The optional operations the registrar that holds a domain offers; a screen shows an action only when it is on.</summary>
/// <param name="DnsManagement">Whether the registrar can switch DNS management on and off for a domain.</param>
/// <param name="EmailForwarding">Whether the registrar can forward a domain's mail.</param>
/// <param name="Whois">Whether the registrar can answer a WHOIS lookup.</param>
public sealed record RegistrarCapabilitiesDto(bool DnsManagement, bool EmailForwarding, bool Whois);
