namespace Innovayse.Hostpanel.Application.Domains.Common;

/// <summary>
/// What the client manages on their own domain: the DNS and email forwarding switches, the
/// nameservers, the DNS records and the forwarding rules.
/// </summary>
/// <param name="DnsManagement">Whether DNS management is on.</param>
/// <param name="EmailForwarding">Whether email forwarding is on.</param>
/// <param name="Nameservers">The domain's nameservers.</param>
/// <param name="DnsRecords">The domain's DNS records.</param>
/// <param name="EmailForwardingRules">The domain's email forwarding rules.</param>
/// <param name="Capabilities">The optional operations the registrar that holds the domain offers; the portal shows an action only when it is on.</param>
public sealed record MyDomainManagementDto(
    bool DnsManagement,
    bool EmailForwarding,
    IReadOnlyList<NameserverDto> Nameservers,
    IReadOnlyList<DnsRecordDto> DnsRecords,
    IReadOnlyList<EmailForwardingRuleDto> EmailForwardingRules,
    RegistrarCapabilitiesDto Capabilities);
