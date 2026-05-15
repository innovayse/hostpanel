namespace Innovayse.Application.Services.DTOs;

using Innovayse.Domain.Services;

/// <summary>Enriched service DTO for admin list views, including client and domain info.</summary>
/// <param name="Id">Primary key of the client service.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="ClientName">Full name of the owning client.</param>
/// <param name="ProductName">Denormalized product display name.</param>
/// <param name="Domain">Linked domain name, or <see langword="null"/> if no domain is linked.</param>
/// <param name="Price">Resolved price based on the billing cycle (monthly or annual).</param>
/// <param name="PriceCurrency">ISO 4217 currency code (e.g. "USD").</param>
/// <param name="BillingCycle">Billing cycle: "monthly" or "annual".</param>
/// <param name="Status">Current lifecycle status of the service.</param>
/// <param name="NextDueDate">Next renewal date, or <see langword="null"/> if not yet active.</param>
public record ServiceListItemDto(
    int Id,
    int ClientId,
    string ClientName,
    string ProductName,
    string? Domain,
    decimal Price,
    string PriceCurrency,
    string BillingCycle,
    ServiceStatus Status,
    DateTimeOffset? NextDueDate);
