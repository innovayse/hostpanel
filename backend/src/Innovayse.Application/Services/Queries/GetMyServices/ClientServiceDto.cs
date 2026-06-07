namespace Innovayse.Application.Services.Queries.GetMyServices;

using Innovayse.Domain.Products;
using Innovayse.Domain.Services;

/// <summary>Represents a client service in list responses.</summary>
/// <param name="Id">Primary key.</param>
/// <param name="ProductId">FK to the ordered product.</param>
/// <param name="ProductName">Denormalized product name for display.</param>
/// <param name="ProductType">The type of product this service is for.</param>
/// <param name="BillingCycle">Billing cycle: "monthly" or "annual".</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="NextRenewalAt">Next renewal date, or <see langword="null"/> if not yet active.</param>
/// <param name="Domain">Linked domain name.</param>
/// <param name="Username">Hosting account username.</param>
/// <param name="Price">Recurring charge amount.</param>
/// <param name="ServerIp">IP address of the assigned server.</param>
/// <param name="ServerHostname">Hostname of the assigned server.</param>
/// <param name="Ns1">Primary nameserver hostname.</param>
/// <param name="Ns2">Secondary nameserver hostname.</param>
/// <param name="TouchEstatePublicKey">Customer's TouchEstate public API key (managed sites only).</param>
/// <param name="TouchEstateSecretKey">Customer's TouchEstate secret API key (managed sites only).</param>
public record ClientServiceDto(
    int Id,
    int ProductId,
    string ProductName,
    ProductType ProductType,
    string BillingCycle,
    ServiceStatus Status,
    DateTimeOffset? NextRenewalAt,
    string? Domain,
    string? Username,
    decimal Price,
    string? ServerIp,
    string? ServerHostname,
    string? Ns1,
    string? Ns2,
    string? TouchEstatePublicKey,
    string? TouchEstateSecretKey);
