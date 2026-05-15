namespace Innovayse.Application.Services.DTOs;

/// <summary>Detailed service DTO for admin detail views, including all editable and read-only fields.</summary>
/// <param name="Id">Primary key of the client service.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="ClientName">Full name of the owning client.</param>
/// <param name="ProductId">FK to the ordered product.</param>
/// <param name="ProductName">Denormalized product display name.</param>
/// <param name="Domain">Linked domain name, or <see langword="null"/> if no domain is linked.</param>
/// <param name="DedicatedIp">Dedicated IP address.</param>
/// <param name="Username">Hosting account username.</param>
/// <param name="Password">Hosting account password.</param>
/// <param name="Quantity">Quantity ordered.</param>
/// <param name="FirstPaymentAmount">First payment amount.</param>
/// <param name="RecurringAmount">Recurring charge amount.</param>
/// <param name="PaymentMethod">Payment method for this service.</param>
/// <param name="PromotionCode">Applied promotion code.</param>
/// <param name="SubscriptionId">External subscription reference.</param>
/// <param name="BillingCycle">Billing cycle: "monthly" or "annual".</param>
/// <param name="Status">Current lifecycle status as string.</param>
/// <param name="ProvisioningRef">External provisioning reference (e.g., cPanel account name).</param>
/// <param name="NextDueDate">Next renewal date, or <see langword="null"/> if not yet active.</param>
/// <param name="CreatedAt">UTC timestamp when the service was ordered.</param>
/// <param name="TerminatedAt">UTC timestamp when the service was terminated.</param>
/// <param name="OverrideAutoSuspend">Whether auto-suspend is overridden.</param>
/// <param name="SuspendUntil">Date until which auto-suspend is blocked.</param>
/// <param name="AutoTerminateEndOfCycle">Whether to auto-terminate at end of billing cycle.</param>
/// <param name="AutoTerminateReason">Reason for auto-termination.</param>
/// <param name="AdminNotes">Internal admin notes.</param>
public record ServiceDetailDto(
    int Id,
    int ClientId,
    string ClientName,
    int ProductId,
    string ProductName,
    string? Domain,
    string? DedicatedIp,
    string? Username,
    string? Password,
    int Quantity,
    decimal FirstPaymentAmount,
    decimal RecurringAmount,
    string? PaymentMethod,
    string? PromotionCode,
    string? SubscriptionId,
    string BillingCycle,
    string Status,
    string? ProvisioningRef,
    DateTimeOffset? NextDueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? TerminatedAt,
    bool OverrideAutoSuspend,
    DateTimeOffset? SuspendUntil,
    bool AutoTerminateEndOfCycle,
    string? AutoTerminateReason,
    string? AdminNotes);
