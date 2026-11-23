using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;

namespace Innovayse.Hostpanel.Application.Domains.Extensions;

/// <summary>
/// Maps a <see cref="RegisteredDomain"/> to the staff <see cref="DomainDto"/> and to the client's own
/// <see cref="MyDomainDto"/>; each read of a domain goes through one of these two mappings.
/// </summary>
public static class DomainMappingExtensions
{
    /// <summary>Maps a domain, with its already-loaded child collections, to its full DTO.</summary>
    /// <param name="domain">The domain; its nameservers, DNS records, forwarding rules and reminders must be loaded.</param>
    /// <param name="capabilities">The optional operations the registrar that holds the domain offers.</param>
    /// <returns>The mapped DTO.</returns>
    public static DomainDto ToDto(this RegisteredDomain domain, RegistrarCapabilitiesDto capabilities)
    {
        ArgumentNullException.ThrowIfNull(domain);

        return new DomainDto(
            domain.Id,
            domain.ClientId,
            domain.Name,
            domain.Tld,
            domain.Status,
            domain.AdminNotes,
            new DomainTermDto(domain.RegisteredAt, domain.ExpiresAt, domain.NextDueDate, domain.RegistrationPeriod),
            new DomainRegistrarDto(domain.Registrar, domain.RegistrarRef, domain.EppCode),
            new DomainFeaturesDto(
                domain.AutoRenew,
                domain.WhoisPrivacy,
                domain.IsLocked,
                domain.DnsManagement,
                domain.EmailForwarding),
            new DomainBillingDto(
                domain.FirstPaymentAmount,
                domain.RecurringAmount,
                domain.PriceCurrency,
                domain.PaymentMethod,
                domain.PromotionCode,
                domain.SubscriptionId),
            new DomainOriginDto(domain.OrderId, domain.OrderType, domain.LinkedServiceId),
            domain.Nameservers.Select(ToNameserverDto).ToList(),
            domain.DnsRecords.Select(ToDnsRecordDto).ToList(),
            domain.EmailForwardingRules.Select(ToEmailForwardingRuleDto).ToList(),
            domain.Reminders
                .Select(r => new DomainReminderDto(r.Id, r.ReminderType, r.SentTo, r.SentAt))
                .ToList(),
            capabilities);
    }

    /// <summary>Maps a domain to the client's own <see cref="MyDomainDto"/>.</summary>
    /// <param name="domain">The domain; its nameservers, DNS records and forwarding rules must be loaded.</param>
    /// <param name="capabilities">The optional operations the registrar that holds the domain offers.</param>
    /// <returns>The mapped DTO.</returns>
    public static MyDomainDto ToMyDto(this RegisteredDomain domain, RegistrarCapabilitiesDto capabilities)
    {
        ArgumentNullException.ThrowIfNull(domain);

        return new MyDomainDto(
            domain.Id,
            domain.Name,
            domain.Status,
            domain.RegisteredAt,
            domain.ExpiresAt,
            domain.NextDueDate,
            domain.AutoRenew,
            domain.WhoisPrivacy,
            domain.IsLocked,
            domain.FirstPaymentAmount,
            domain.RecurringAmount,
            domain.PriceCurrency,
            domain.PaymentMethod,
            domain.RegistrationPeriod,
            new MyDomainManagementDto(
                domain.DnsManagement,
                domain.EmailForwarding,
                domain.Nameservers.Select(ToNameserverDto).ToList(),
                domain.DnsRecords.Select(ToDnsRecordDto).ToList(),
                domain.EmailForwardingRules.Select(ToEmailForwardingRuleDto).ToList(),
                capabilities));
    }

    /// <summary>Maps one nameserver to <see cref="NameserverDto"/>.</summary>
    /// <param name="nameserver">The domain's nameserver entry.</param>
    /// <returns>The mapped DTO.</returns>
    private static NameserverDto ToNameserverDto(Nameserver nameserver) =>
        new(nameserver.Id, nameserver.Host);

    /// <summary>Maps one DNS record to <see cref="DnsRecordDto"/>.</summary>
    /// <param name="record">The DNS record.</param>
    /// <returns>The mapped DTO.</returns>
    private static DnsRecordDto ToDnsRecordDto(DnsRecord record) =>
        new(record.Id, record.Type, record.Host, record.Value, record.Ttl, record.Priority);

    /// <summary>Maps one forwarding rule to <see cref="EmailForwardingRuleDto"/>.</summary>
    /// <param name="rule">The forwarding rule.</param>
    /// <returns>The mapped DTO.</returns>
    private static EmailForwardingRuleDto ToEmailForwardingRuleDto(EmailForwardingRule rule) =>
        new(rule.Id, rule.Source, rule.Destination, rule.IsActive);
}
