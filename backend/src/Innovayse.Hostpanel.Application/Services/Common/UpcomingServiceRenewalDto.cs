using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>One active service renewing inside the window staff are looking at, with its auto-renew switch.</summary>
/// <param name="Id">The service.</param>
/// <param name="ClientId">The client who owns it.</param>
/// <param name="ClientName">Full name of the owning client.</param>
/// <param name="ProductName">Name of the ordered product.</param>
/// <param name="Domain">The domain the service runs on, or <see langword="null"/> when it has none.</param>
/// <param name="Amount">The recurring price of the period to bill.</param>
/// <param name="PriceCurrency">ISO 4217 code of the owning client's billing currency.</param>
/// <param name="BillingCycle">The billing cycle.</param>
/// <param name="NextRenewalAt">Start of the next period to bill.</param>
/// <param name="AutoRenew">Whether the billing runs look after the service; <see langword="false"/> means staff bill it by hand.</param>
public sealed record UpcomingServiceRenewalDto(
    int Id,
    int ClientId,
    string ClientName,
    string ProductName,
    string? Domain,
    decimal Amount,
    string PriceCurrency,
    BillingCycle BillingCycle,
    DateTimeOffset NextRenewalAt,
    bool AutoRenew);
