using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>Enriched service DTO for admin list views, including client and domain info.</summary>
/// <param name="Id">Primary key of the client service.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="ClientName">Full name of the owning client; every service has one, since its client cannot be deleted.</param>
/// <param name="ProductName">Name of the ordered product; every service has one, since its product cannot be deleted.</param>
/// <param name="Domain">Linked domain name, or <see langword="null"/> if no domain is linked.</param>
/// <param name="Price">Resolved price based on the billing cycle (monthly or annual).</param>
/// <param name="PriceCurrency">ISO 4217 currency code (e.g. "USD").</param>
/// <param name="BillingCycle">The billing cycle.</param>
/// <param name="Status">Current lifecycle status of the service.</param>
/// <param name="NextDueDate">Next renewal date, or <see langword="null"/> if not yet active.</param>
/// <param name="TrialStatus">Where the service is in its free trial, or <see langword="null"/> when it is not a trial.</param>
/// <param name="TrialEndsAt">When the free trial ends, or <see langword="null"/> when it is not a trial.</param>
/// <param name="TrialDaysLeft">Whole days left of an active trial, rounded up and never negative; <see langword="null"/> unless the trial is active.</param>
public sealed record ServiceListItemDto(
    int Id,
    int ClientId,
    string ClientName,
    string ProductName,
    string? Domain,
    decimal Price,
    string PriceCurrency,
    BillingCycle BillingCycle,
    ServiceStatus Status,
    DateTimeOffset? NextDueDate,
    TrialStatus? TrialStatus,
    DateTimeOffset? TrialEndsAt,
    int? TrialDaysLeft);
