using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>Represents a client service in list responses.</summary>
/// <param name="Id">Primary key.</param>
/// <param name="ProductId">FK to the ordered product.</param>
/// <param name="ProductName">Name of the ordered product; every service has one, since its product cannot be deleted.</param>
/// <param name="Sku">The ordered product's SKU, or <see langword="null"/> when the product has none.</param>
/// <param name="IsDigital">Whether the product is a digital one, delivered by fulfilment plugins: it has nothing to set up on a server, so the client area never asks for setup.</param>
/// <param name="BillingCycle">The billing cycle.</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="NextRenewalAt">Next renewal date, or <see langword="null"/> if not yet active.</param>
/// <param name="Domain">Linked domain name.</param>
/// <param name="Username">Hosting account username.</param>
/// <param name="Price">Recurring charge amount.</param>
/// <param name="FirstPaymentAmount">Amount charged on the first invoice.</param>
/// <param name="PaymentMethod">Payment method used, or <see langword="null"/> if not set.</param>
/// <param name="Server">The assigned server, or <see langword="null"/> if none is assigned.</param>
/// <param name="CreatedAt">When the service was ordered, shown as its registration date.</param>
/// <param name="SuspensionSource">Why the service is suspended, or <see langword="null"/> when it is not.</param>
/// <param name="Usage">The latest disk and bandwidth figures the server reported, or <see langword="null"/> when it reported none.</param>
public sealed record ClientServiceDto(
    int Id,
    int ProductId,
    string ProductName,
    string? Sku,
    bool IsDigital,
    BillingCycle BillingCycle,
    ServiceStatus Status,
    DateTimeOffset? NextRenewalAt,
    string? Domain,
    string? Username,
    decimal Price,
    decimal FirstPaymentAmount,
    string? PaymentMethod,
    ServiceServerDto? Server,
    DateTimeOffset CreatedAt,
    SuspensionSource? SuspensionSource,
    ServiceUsageDto? Usage);
