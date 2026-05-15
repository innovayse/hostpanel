namespace Innovayse.Application.Services.Queries.GetMyServices;

using Innovayse.Domain.Services;

/// <summary>Represents a client service in list responses.</summary>
/// <param name="Id">Primary key.</param>
/// <param name="ProductId">FK to the ordered product.</param>
/// <param name="ProductName">Denormalized product name for display.</param>
/// <param name="BillingCycle">Billing cycle: "monthly" or "annual".</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="NextRenewalAt">Next renewal date, or <see langword="null"/> if not yet active.</param>
public record ClientServiceDto(
    int Id,
    int ProductId,
    string ProductName,
    string BillingCycle,
    ServiceStatus Status,
    DateTimeOffset? NextRenewalAt);
