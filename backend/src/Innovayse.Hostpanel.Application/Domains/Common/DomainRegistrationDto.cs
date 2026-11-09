using Innovayse.Hostpanel.Domain.Domains.Enums;

namespace Innovayse.Hostpanel.Application.Domains.Common;

/// <summary>DTO for domain registration list: includes client name, pricing, and registrar info.</summary>
/// <param name="Id">Domain primary key.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="ClientName">Full name of the owning client; every domain has one, since its client cannot be deleted.</param>
/// <param name="Name">Domain name including TLD (e.g. "example.com").</param>
/// <param name="RegPeriodYears">Registration period in whole years; the caller formats it for display.</param>
/// <param name="Registrar">Name of the registrar module.</param>
/// <param name="RecurringAmount">Recurring registration price.</param>
/// <param name="PriceCurrency">ISO 4217 currency code for the price.</param>
/// <param name="NextDueDate">Next renewal payment due date (UTC).</param>
/// <param name="ExpiresAt">Domain expiration date (UTC).</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="AutoRenew">Whether the domain is set to auto-renew.</param>
public sealed record DomainRegistrationDto(
    int Id,
    int ClientId,
    string ClientName,
    string Name,
    int RegPeriodYears,
    string? Registrar,
    decimal RecurringAmount,
    string PriceCurrency,
    DateTimeOffset NextDueDate,
    DateTimeOffset ExpiresAt,
    DomainStatus Status,
    bool AutoRenew);
