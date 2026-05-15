namespace Innovayse.Application.Domains.DTOs;

using Innovayse.Domain.Domains;

/// <summary>DTO representing a full domain with all details, nameservers, and DNS records.</summary>
/// <param name="Id">Domain primary key.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="Name">Domain name without TLD (e.g. "example").</param>
/// <param name="Tld">Top-level domain including the dot (e.g. ".com").</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="RegisteredAt">Domain registration date (UTC).</param>
/// <param name="ExpiresAt">Domain expiration date (UTC).</param>
/// <param name="AutoRenew">Whether the domain is set to auto-renew at expiration.</param>
/// <param name="WhoisPrivacy">Whether WHOIS privacy is enabled.</param>
/// <param name="IsLocked">Whether the domain is locked against unauthorized transfers.</param>
/// <param name="RegistrarRef">Reference ID from the registrar's system; null if not yet registered.</param>
/// <param name="EppCode">Authorization code for transfer; null if not available.</param>
/// <param name="LinkedServiceId">FK to linked service (e.g. hosting plan); null if none.</param>
/// <param name="FirstPaymentAmount">One-time registration cost.</param>
/// <param name="RecurringAmount">Recurring registration price.</param>
/// <param name="PaymentMethod">Payment method label (e.g. "Credit/Debit Card"); null if not set.</param>
/// <param name="PromotionCode">Applied promotion/coupon code; null if none.</param>
/// <param name="SubscriptionId">External payment subscription reference; null if none.</param>
/// <param name="AdminNotes">Free-text admin notes; null if empty.</param>
/// <param name="OrderId">FK to the order that created this domain; null if unknown.</param>
/// <param name="OrderType">Order type: "Register" or "Transfer".</param>
/// <param name="DnsManagement">Whether DNS management is enabled.</param>
/// <param name="EmailForwarding">Whether email forwarding is enabled.</param>
/// <param name="PriceCurrency">ISO 4217 currency code for the price.</param>
/// <param name="NextDueDate">Next renewal payment due date (UTC).</param>
/// <param name="Registrar">Name of the registrar module; null if not set.</param>
/// <param name="RegistrationPeriod">Registration period in years.</param>
/// <param name="Nameservers">Assigned name servers for this domain.</param>
/// <param name="DnsRecords">DNS records configured for this domain.</param>
/// <param name="EmailForwardingRules">Email forwarding rules configured for this domain.</param>
/// <param name="Reminders">Expiry reminders sent for this domain.</param>
public record DomainDto(
    int Id,
    int ClientId,
    string Name,
    string Tld,
    DomainStatus Status,
    DateTimeOffset RegisteredAt,
    DateTimeOffset ExpiresAt,
    bool AutoRenew,
    bool WhoisPrivacy,
    bool IsLocked,
    string? RegistrarRef,
    string? EppCode,
    int? LinkedServiceId,
    decimal FirstPaymentAmount,
    decimal RecurringAmount,
    string? PaymentMethod,
    string? PromotionCode,
    string? SubscriptionId,
    string? AdminNotes,
    int? OrderId,
    string OrderType,
    bool DnsManagement,
    bool EmailForwarding,
    string PriceCurrency,
    DateTimeOffset NextDueDate,
    string? Registrar,
    int RegistrationPeriod,
    IReadOnlyList<NameserverDto> Nameservers,
    IReadOnlyList<DnsRecordDto> DnsRecords,
    IReadOnlyList<EmailForwardingRuleDto> EmailForwardingRules,
    IReadOnlyList<DomainReminderDto> Reminders);
