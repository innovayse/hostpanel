namespace Innovayse.API.Services.Requests;

/// <summary>HTTP request body for PUT /api/services/{id}.</summary>
/// <param name="Domain">Linked domain name (null to clear).</param>
/// <param name="DedicatedIp">Dedicated IP address (null to clear).</param>
/// <param name="Username">Hosting account username (null to clear).</param>
/// <param name="Password">Hosting account password (null to clear).</param>
/// <param name="BillingCycle">Billing cycle: "monthly" or "annual".</param>
/// <param name="RecurringAmount">Recurring charge amount.</param>
/// <param name="PaymentMethod">Payment method for this service (null to clear).</param>
/// <param name="NextRenewalAt">Next renewal date (null to clear).</param>
/// <param name="SubscriptionId">External subscription reference (null to clear).</param>
/// <param name="OverrideAutoSuspend">Whether auto-suspend is overridden.</param>
/// <param name="SuspendUntil">Date until which auto-suspend is blocked (null to clear).</param>
/// <param name="AutoTerminateEndOfCycle">Whether to auto-terminate at end of billing cycle.</param>
/// <param name="AutoTerminateReason">Reason for auto-termination (null to clear).</param>
/// <param name="AdminNotes">Internal admin notes (null to clear).</param>
/// <param name="ProvisioningRef">Server/provisioning reference (null to clear).</param>
/// <param name="FirstPaymentAmount">First payment amount.</param>
/// <param name="PromotionCode">Promotion/coupon code (null to clear).</param>
/// <param name="TerminatedAt">Termination date — admin override (null to clear).</param>
/// <param name="Status">Service status (Active, Suspended, Terminated). Null to keep current.</param>
public record UpdateServiceRequest(
    string? Domain,
    string? DedicatedIp,
    string? Username,
    string? Password,
    string BillingCycle,
    decimal RecurringAmount,
    string? PaymentMethod,
    DateTimeOffset? NextRenewalAt,
    string? SubscriptionId,
    bool OverrideAutoSuspend,
    DateTimeOffset? SuspendUntil,
    bool AutoTerminateEndOfCycle,
    string? AutoTerminateReason,
    string? AdminNotes,
    string? ProvisioningRef,
    decimal FirstPaymentAmount,
    string? PromotionCode,
    DateTimeOffset? TerminatedAt,
    string? Status);
