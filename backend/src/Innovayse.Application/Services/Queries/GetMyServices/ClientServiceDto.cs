namespace Innovayse.Application.Services.Queries.GetMyServices;

using Innovayse.Domain.Services;

/// <summary>Represents a client service in list responses.</summary>
/// <param name="Id">Primary key.</param>
/// <param name="ProductId">FK to the ordered product.</param>
/// <param name="ProductName">Denormalized product name for display.</param>
/// <param name="BillingCycle">Billing cycle: "monthly" or "annual".</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="NextRenewalAt">Next renewal date, or <see langword="null"/> if not yet active.</param>
/// <param name="Domain">Linked domain name.</param>
/// <param name="Username">Hosting account username.</param>
/// <param name="Price">Recurring charge amount.</param>
/// <param name="FirstPaymentAmount">Amount charged on the first invoice.</param>
/// <param name="PaymentMethod">Payment method used, or <see langword="null"/> if not set.</param>
/// <param name="ServerId">FK to the provisioning server, or <see langword="null"/> if not assigned.</param>
/// <param name="ServerName">Display name of the assigned server, or <see langword="null"/> if not assigned.</param>
/// <param name="ServerHostname">Hostname of the assigned server, or <see langword="null"/> if not assigned.</param>
/// <param name="ServerIp">IP address of the assigned server, or <see langword="null"/> if not assigned.</param>
public record ClientServiceDto(
    int Id,
    int ProductId,
    string ProductName,
    string BillingCycle,
    ServiceStatus Status,
    DateTimeOffset? NextRenewalAt,
    string? Domain,
    string? Username,
    decimal Price,
    decimal FirstPaymentAmount,
    string? PaymentMethod,
    int? ServerId,
    string? ServerName,
    string? ServerHostname,
    string? ServerIp);
