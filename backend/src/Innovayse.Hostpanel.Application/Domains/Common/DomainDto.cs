using Innovayse.Hostpanel.Domain.Domains.Enums;

namespace Innovayse.Hostpanel.Application.Domains.Common;

/// <summary>DTO representing a full domain with all details, nameservers, and DNS records.</summary>
/// <param name="Id">Domain primary key.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="Name">Full domain name including the TLD (e.g. "example.com"), as stored on the entity.</param>
/// <param name="Tld">Top-level domain including the dot (e.g. ".com").</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="AdminNotes">Free-text admin notes; null if empty.</param>
/// <param name="Term">Registration, expiry and renewal dates and the registration period.</param>
/// <param name="Registrar">The registrar and the identifiers it holds for the domain.</param>
/// <param name="Features">The auto-renew, privacy, lock, DNS and forwarding switches.</param>
/// <param name="Billing">Prices, currency, payment method, promotion and subscription.</param>
/// <param name="Origin">The order that created the domain and the service it is linked to.</param>
/// <param name="Nameservers">Assigned name servers for this domain.</param>
/// <param name="DnsRecords">DNS records configured for this domain.</param>
/// <param name="EmailForwardingRules">Email forwarding rules configured for this domain.</param>
/// <param name="Reminders">Expiry reminders sent for this domain.</param>
/// <param name="Capabilities">The optional operations the registrar that holds the domain offers.</param>
public record DomainDto(
    int Id,
    int ClientId,
    string Name,
    string Tld,
    DomainStatus Status,
    string? AdminNotes,
    DomainTermDto Term,
    DomainRegistrarDto Registrar,
    DomainFeaturesDto Features,
    DomainBillingDto Billing,
    DomainOriginDto Origin,
    IReadOnlyList<NameserverDto> Nameservers,
    IReadOnlyList<DnsRecordDto> DnsRecords,
    IReadOnlyList<EmailForwardingRuleDto> EmailForwardingRules,
    IReadOnlyList<DomainReminderDto> Reminders,
    RegistrarCapabilitiesDto Capabilities);
