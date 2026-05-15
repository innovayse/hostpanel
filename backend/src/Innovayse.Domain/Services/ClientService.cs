namespace Innovayse.Domain.Services;

using Innovayse.Domain.Common;
using Innovayse.Domain.Provisioning.Events;
using Innovayse.Domain.Services.Events;

/// <summary>
/// Represents a service instance owned by a client.
/// Links a <see cref="ClientId"/> to a product and tracks provisioning state.
/// Stored in the <c>client_services</c> table.
/// </summary>
public sealed class ClientService : AggregateRoot
{
    /// <summary>Gets the FK to the owning client.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the FK to the ordered product.</summary>
    public int ProductId { get; private set; }

    /// <summary>Gets the selected billing cycle ("monthly" or "annual").</summary>
    public string BillingCycle { get; private set; } = string.Empty;

    /// <summary>Gets the current lifecycle status.</summary>
    public ServiceStatus Status { get; private set; }

    /// <summary>Gets the external provisioning reference (e.g., cPanel account name).</summary>
    public string? ProvisioningRef { get; private set; }

    /// <summary>Gets the UTC timestamp when the service was ordered.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets the UTC timestamp of the next renewal date, if active.</summary>
    public DateTimeOffset? NextRenewalAt { get; private set; }

    /// <summary>Gets the linked domain name.</summary>
    public string? Domain { get; private set; }

    /// <summary>Gets the dedicated IP address.</summary>
    public string? DedicatedIp { get; private set; }

    /// <summary>Gets the hosting account username.</summary>
    public string? Username { get; private set; }

    /// <summary>Gets the hosting account password.</summary>
    public string? Password { get; private set; }

    /// <summary>Gets the quantity ordered.</summary>
    public int Quantity { get; private set; } = 1;

    /// <summary>Gets the first payment amount.</summary>
    public decimal FirstPaymentAmount { get; private set; }

    /// <summary>Gets the recurring charge amount.</summary>
    public decimal RecurringAmount { get; private set; }

    /// <summary>Gets the payment method for this service.</summary>
    public string? PaymentMethod { get; private set; }

    /// <summary>Gets the applied promotion code.</summary>
    public string? PromotionCode { get; private set; }

    /// <summary>Gets the external subscription reference.</summary>
    public string? SubscriptionId { get; private set; }

    /// <summary>Gets when the service was terminated.</summary>
    public DateTimeOffset? TerminatedAt { get; private set; }

    /// <summary>Gets whether auto-suspend is overridden.</summary>
    public bool OverrideAutoSuspend { get; private set; }

    /// <summary>Gets the date until which auto-suspend is blocked.</summary>
    public DateTimeOffset? SuspendUntil { get; private set; }

    /// <summary>Gets whether to auto-terminate at end of billing cycle.</summary>
    public bool AutoTerminateEndOfCycle { get; private set; }

    /// <summary>Gets the reason for auto-termination.</summary>
    public string? AutoTerminateReason { get; private set; }

    /// <summary>Gets the internal admin notes.</summary>
    public string? AdminNotes { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private ClientService() : base(0) { }

    /// <summary>
    /// Creates a new pending client service and raises <see cref="ClientServiceCreatedEvent"/>.
    /// </summary>
    /// <param name="clientId">FK to the client placing the order.</param>
    /// <param name="productId">FK to the product being ordered.</param>
    /// <param name="billingCycle">Billing cycle: "monthly" or "annual".</param>
    /// <returns>A new pending <see cref="ClientService"/>.</returns>
    public static ClientService Create(int clientId, int productId, string billingCycle)
    {
        var svc = new ClientService
        {
            ClientId = clientId,
            ProductId = productId,
            BillingCycle = billingCycle,
            Status = ServiceStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        svc.AddDomainEvent(new ClientServiceCreatedEvent(0, clientId, productId));
        return svc;
    }

    /// <summary>
    /// Marks the service as active after successful provisioning.
    /// Sets the provisioning reference and calculates the first renewal date.
    /// </summary>
    /// <param name="provisioningRef">External reference from the provisioning provider.</param>
    /// <exception cref="InvalidOperationException">Thrown when the service is not in Pending status.</exception>
    public void Activate(string provisioningRef)
    {
        if (Status is not ServiceStatus.Pending)
            throw new InvalidOperationException($"Cannot activate a service with status {Status}. Only Pending services can be activated.");

        Status = ServiceStatus.Active;
        ProvisioningRef = provisioningRef;
        NextRenewalAt = BillingCycle == "annual"
            ? CreatedAt.AddYears(1)
            : CreatedAt.AddMonths(1);
        AddDomainEvent(new ServiceProvisionedEvent(Id, ClientId, provisioningRef));
    }

    /// <summary>
    /// Suspends the service and raises <see cref="ClientServiceSuspendedEvent"/>
    /// and <see cref="ServiceSuspendedEvent"/>.
    /// </summary>
    /// <param name="reason">Human-readable reason for the suspension.</param>
    /// <exception cref="InvalidOperationException">Thrown when the service is not in Active status.</exception>
    public void Suspend(string reason = "")
    {
        if (Status is not ServiceStatus.Active)
            throw new InvalidOperationException($"Cannot suspend a service with status {Status}. Only Active services can be suspended.");

        Status = ServiceStatus.Suspended;
        AddDomainEvent(new ClientServiceSuspendedEvent(Id, ClientId));
        AddDomainEvent(new ServiceSuspendedEvent(Id, ClientId, reason));
    }

    /// <summary>
    /// Terminates the service and raises <see cref="ClientServiceTerminatedEvent"/>
    /// and <see cref="ServiceTerminatedEvent"/>.
    /// </summary>
    /// <param name="reason">Human-readable reason for the termination.</param>
    /// <exception cref="InvalidOperationException">Thrown when the service is already terminated.</exception>
    public void Terminate(string reason = "")
    {
        if (Status is ServiceStatus.Terminated)
            throw new InvalidOperationException("Service is already terminated.");

        Status = ServiceStatus.Terminated;
        TerminatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new ClientServiceTerminatedEvent(Id, ClientId));
        AddDomainEvent(new ServiceTerminatedEvent(Id, ClientId, reason));
    }

    /// <summary>
    /// Re-activates a previously suspended service, restoring it to active status.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the service is not in Suspended status.</exception>
    public void Unsuspend()
    {
        if (Status is not ServiceStatus.Suspended)
            throw new InvalidOperationException($"Cannot unsuspend a service with status {Status}. Only Suspended services can be unsuspended.");

        Status = ServiceStatus.Active;
    }

    /// <summary>
    /// Updates the editable service fields.
    /// </summary>
    /// <param name="domain">Linked domain name.</param>
    /// <param name="dedicatedIp">Dedicated IP address.</param>
    /// <param name="username">Hosting account username.</param>
    /// <param name="password">Hosting account password.</param>
    /// <param name="billingCycle">Billing cycle: "monthly" or "annual".</param>
    /// <param name="recurringAmount">Recurring charge amount.</param>
    /// <param name="paymentMethod">Payment method for this service.</param>
    /// <param name="nextRenewalAt">Next renewal date.</param>
    /// <param name="subscriptionId">External subscription reference.</param>
    /// <param name="overrideAutoSuspend">Whether auto-suspend is overridden.</param>
    /// <param name="suspendUntil">Date until which auto-suspend is blocked.</param>
    /// <param name="autoTerminateEndOfCycle">Whether to auto-terminate at end of billing cycle.</param>
    /// <param name="autoTerminateReason">Reason for auto-termination.</param>
    /// <param name="adminNotes">Internal admin notes.</param>
    /// <param name="provisioningRef">Server/provisioning reference.</param>
    /// <param name="firstPaymentAmount">First payment amount.</param>
    /// <param name="promotionCode">Promotion/coupon code.</param>
    /// <param name="terminatedAt">Termination date (admin override).</param>
    public void Update(string? domain, string? dedicatedIp, string? username,
        string? password, string billingCycle, decimal recurringAmount,
        string? paymentMethod, DateTimeOffset? nextRenewalAt,
        string? subscriptionId, bool overrideAutoSuspend,
        DateTimeOffset? suspendUntil, bool autoTerminateEndOfCycle,
        string? autoTerminateReason, string? adminNotes,
        string? provisioningRef, decimal firstPaymentAmount,
        string? promotionCode, DateTimeOffset? terminatedAt)
    {
        Domain = domain;
        DedicatedIp = dedicatedIp;
        Username = username;
        Password = password;
        BillingCycle = billingCycle;
        RecurringAmount = recurringAmount;
        PaymentMethod = paymentMethod;
        NextRenewalAt = nextRenewalAt;
        SubscriptionId = subscriptionId;
        OverrideAutoSuspend = overrideAutoSuspend;
        SuspendUntil = suspendUntil;
        AutoTerminateEndOfCycle = autoTerminateEndOfCycle;
        AutoTerminateReason = autoTerminateReason;
        AdminNotes = adminNotes;
        ProvisioningRef = provisioningRef;
        FirstPaymentAmount = firstPaymentAmount;
        PromotionCode = promotionCode;
        TerminatedAt = terminatedAt;
    }
}
