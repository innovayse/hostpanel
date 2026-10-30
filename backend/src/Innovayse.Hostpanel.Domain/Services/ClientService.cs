using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Provisioning.Events;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Domain.Services.Events;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Services;

/// <summary>
/// Represents a service instance owned by a client.
/// Links a <see cref="ClientId"/> to a product and tracks provisioning state.
/// Stored in the <c>ClientServices</c> table.
/// </summary>
public sealed class ClientService : BaseEntity, IAggregateRoot
{
    /// <summary>Gets the FK to the owning client.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the FK to the ordered product.</summary>
    public int ProductId { get; private set; }

    /// <summary>Gets the selected billing cycle.</summary>
    public BillingCycle BillingCycle { get; private set; }

    /// <summary>Gets the current lifecycle status.</summary>
    public ServiceStatus Status { get; private set; }

    /// <summary>Gets the external provisioning reference (e.g., hosting account name).</summary>
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

    /// <summary>
    /// Gets the id of the invoice the first payment is charged on, or <see langword="null"/> when
    /// the service was ordered without one. Index only, no FK: the invoice belongs to Billing.
    /// </summary>
    public int? FirstInvoiceId { get; private set; }

    /// <summary>
    /// Gets whether the client must pay the first invoice before the service may be set up: true
    /// when the first payment is not free and the service names the invoice it is charged on.
    /// A service without <see cref="FirstInvoiceId"/> (created by an administrator, or ordered
    /// before services recorded their first invoice) has no invoice to check, so it keeps the
    /// earlier behaviour and may be set up; refusing it would lock those services out for good.
    /// </summary>
    public bool RequiresPaidFirstInvoice => FirstPaymentAmount > 0m && FirstInvoiceId is not null;

    /// <summary>Gets the payment method for this service.</summary>
    public string? PaymentMethod { get; private set; }

    /// <summary>Gets the applied promotion code.</summary>
    public string? PromotionCode { get; private set; }

    /// <summary>Gets the external subscription reference.</summary>
    public string? SubscriptionId { get; private set; }

    /// <summary>Gets the UTC timestamp when the service was suspended, or <see langword="null"/> if not suspended.</summary>
    public DateTimeOffset? SuspendedAt { get; private set; }

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

    /// <summary>Gets the FK to the provisioning server, or null if not assigned.</summary>
    public int? ServerId { get; private set; }

    /// <summary>Gets the internal admin notes.</summary>
    public string? AdminNotes { get; private set; }

    /// <summary>
    /// Gets whether the billing runs look after the service: a renewal invoice is raised ahead of
    /// <see cref="NextRenewalAt"/>, an overdue one suspends it, and a long suspension terminates it.
    /// <see langword="false"/> means staff bill the service by hand, so none of those runs touch it.
    /// </summary>
    public bool AutoRenew { get; private set; } = true;

    /// <summary>Gets why the service is suspended, or <see langword="null"/> when it is not (or the reason predates this column).</summary>
    public SuspensionSource? SuspensionSource { get; private set; }

    /// <summary>Gets whether <see cref="Suspend"/> accepts the service: only an Active one can be suspended.</summary>
    public bool CanSuspend => Status is ServiceStatus.Active;

    /// <summary>Gets whether <see cref="Unsuspend"/> accepts the service: only a Suspended one can be unsuspended.</summary>
    public bool CanUnsuspend => Status is ServiceStatus.Suspended;

    /// <summary>Gets whether <see cref="Terminate"/> accepts the service: any one not terminated already.</summary>
    public bool CanTerminate => Status is not ServiceStatus.Terminated;

    /// <summary>
    /// Whether staff may move the service to <paramref name="target"/>. Asking for the current status
    /// is a no-op and always allowed; otherwise Active and Suspended swap through
    /// <see cref="Suspend"/> and <see cref="Unsuspend"/>, and Pending, Active or Suspended end through
    /// <see cref="Terminate"/>. Nothing reaches Pending, and nothing leaves Terminated.
    /// </summary>
    /// <param name="target">The status asked for.</param>
    /// <returns><see langword="true"/> when the matching transition accepts the service.</returns>
    public bool CanChangeStatusTo(ServiceStatus target)
    {
        if (target == Status)
        {
            return true;
        }

        return target switch
        {
            ServiceStatus.Active => CanUnsuspend,
            ServiceStatus.Suspended => CanSuspend,
            ServiceStatus.Terminated => CanTerminate,
            _ => false,
        };
    }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private ClientService() { }

    /// <summary>
    /// Creates a new pending client service and raises <see cref="ClientServiceCreatedEvent"/>.
    /// </summary>
    /// <param name="clientId">FK to the client placing the order.</param>
    /// <param name="productId">FK to the product being ordered.</param>
    /// <param name="billingCycle">The billing cycle.</param>
    /// <param name="now">The current moment (UTC), read by the caller; stamped on the domain event.</param>
    /// <returns>A new pending <see cref="ClientService"/>.</returns>
    public static ClientService Create(int clientId, int productId, BillingCycle billingCycle, DateTimeOffset now)
    {
        var svc = new ClientService
        {
            ClientId = clientId,
            ProductId = productId,
            BillingCycle = billingCycle,
            Status = ServiceStatus.Pending,
        };
        svc.AddDomainEvent(() => new ClientServiceCreatedEvent(svc.Id, clientId, productId, now));
        return svc;
    }

    /// <summary>
    /// Creates a pending service from an order line, with the amounts and payment method the
    /// order agreed, and raises <see cref="ClientServiceCreatedEvent"/>.
    /// </summary>
    /// <param name="clientId">FK to the client placing the order.</param>
    /// <param name="productId">FK to the product being ordered.</param>
    /// <param name="billingCycle">The billing cycle.</param>
    /// <param name="domain">Domain name bought alongside the hosting, or <see langword="null"/>.</param>
    /// <param name="firstPaymentAmount">One-time amount charged on the first invoice.</param>
    /// <param name="recurringAmount">Amount charged on each renewal invoice.</param>
    /// <param name="paymentMethod">Payment method the order was placed with, or <see langword="null"/>.</param>
    /// <param name="firstInvoiceId">The invoice the first payment is charged on, or <see langword="null"/> when there is none yet.</param>
    /// <param name="now">The current moment (UTC), read by the caller; stamped on the domain event.</param>
    /// <returns>A new pending <see cref="ClientService"/>.</returns>
    public static ClientService CreateOrdered(
        int clientId, int productId, BillingCycle billingCycle, string? domain,
        decimal firstPaymentAmount, decimal recurringAmount, string? paymentMethod, int? firstInvoiceId, DateTimeOffset now)
    {
        var svc = Create(clientId, productId, billingCycle, now);
        svc.Domain = domain;
        svc.FirstInvoiceId = firstInvoiceId;
        svc.FirstPaymentAmount = firstPaymentAmount;
        svc.RecurringAmount = recurringAmount;
        svc.PaymentMethod = paymentMethod;
        return svc;
    }

    /// <summary>
    /// Creates a service in the state another billing system already has it in, raising no domain
    /// event: the client was welcomed and the hosting account set up, suspended or removed there,
    /// so neither the welcome mail nor a panel call may follow from the import.
    /// </summary>
    /// <param name="import">The imported service's state.</param>
    /// <returns>The imported <see cref="ClientService"/>, not yet saved.</returns>
    public static ClientService Import(ServiceImport import)
    {
        ArgumentNullException.ThrowIfNull(import);

        var svc = new ClientService
        {
            ClientId = import.ClientId,
            ProductId = import.ProductId,
            BillingCycle = import.BillingCycle,
            Status = import.Status,
            CreatedAt = import.RegisteredAt,
        };

        // A pending service has not started a billing period yet, as with Activate.
        if (import.Status is not ServiceStatus.Pending)
        {
            svc.NextRenewalAt = RenewalPeriod.EndOf(import.RegisteredAt, import.BillingCycle, import.RegisteredAt.Day);
        }

        if (import.Status is ServiceStatus.Suspended)
        {
            svc.SuspendedAt = import.StatusChangedAt;
            svc.SuspensionSource = import.SuspensionSource;
        }
        else if (import.Status is ServiceStatus.Terminated)
        {
            svc.TerminatedAt = import.StatusChangedAt;
        }

        svc.RecordHostingAccount(import.HostingAccount);
        return svc;
    }

    /// <summary>Moves the creation date to the moment the service was really ordered, for services seeded from history.</summary>
    /// <param name="createdAt">The moment the service was ordered (UTC).</param>
    public void Backdate(DateTimeOffset createdAt) => CreatedAt = createdAt;

    /// <summary>
    /// Marks the service as active after successful provisioning.
    /// Sets the provisioning reference and calculates the first renewal date.
    /// </summary>
    /// <param name="provisioningRef">External reference from the provisioning provider.</param>
    /// <param name="billingAnchor">The moment the billing period starts from (UTC): the activation moment for a provisioned service, or the registration date of an imported one.</param>
    /// <param name="now">The current moment (UTC), read by the caller; stamped on the domain event.</param>
    /// <exception cref="DomainRuleException">
    /// Thrown with <see cref="ServicesErrors.ServiceNotPending"/> when the service is not in Pending status.
    /// </exception>
    public void Activate(string provisioningRef, DateTimeOffset billingAnchor, DateTimeOffset now)
    {
        StartBilling(billingAnchor);
        ProvisioningRef = provisioningRef;

        AddDomainEvent(new ServiceProvisionedEvent(Id, ClientId, provisioningRef, now));
    }

    /// <summary>
    /// Activates a pending service of a digital product, which has no hosting account to provision: it turns
    /// Active and its first billing period starts, the same as <see cref="Activate"/>, but it gets no
    /// provisioning reference and no server, and raises no <see cref="ServiceProvisionedEvent"/>.
    /// </summary>
    /// <remarks>
    /// The provisioning event would send the client the hosting account mail, and nothing was set up on a
    /// panel. What a digital product delivers is told to fulfilment plugins by its paid invoice, not by
    /// this activation. The caller decides the product is digital; the service does not hold that fact.
    /// </remarks>
    /// <param name="billingAnchor">The moment the billing period starts from (UTC): the activation moment.</param>
    /// <exception cref="DomainRuleException">
    /// Thrown with <see cref="ServicesErrors.ServiceNotPending"/> when the service is not in Pending status.
    /// </exception>
    public void ActivateDigital(DateTimeOffset billingAnchor)
    {
        StartBilling(billingAnchor);
    }

    /// <summary>
    /// The half of activation every pending service shares, provisioned or digital: refuses anything but
    /// Pending, turns the service Active and starts its first billing period from the anchor.
    /// </summary>
    /// <param name="billingAnchor">The moment the billing period starts from (UTC).</param>
    /// <exception cref="DomainRuleException">
    /// Thrown with <see cref="ServicesErrors.ServiceNotPending"/> when the service is not in Pending status.
    /// </exception>
    private void StartBilling(DateTimeOffset billingAnchor)
    {
        if (Status is not ServiceStatus.Pending)
        {
            throw new DomainRuleException(
                ServicesErrors.ServiceNotPending,
                $"Cannot activate a service with status {Status}. Only Pending services can be activated.");
        }

        Status = ServiceStatus.Active;

        // A renewal date already later than the first period's end (staff set it while the
        // service was pending) is kept, the same rule as ExtendRenewalTo.
        ExtendRenewalTo(RenewalPeriod.EndOf(billingAnchor, BillingCycle, billingAnchor.Day));
    }

    /// <summary>
    /// Records the server the account was provisioned on and activates the service, as one
    /// transition.
    /// </summary>
    /// <remarks>
    /// <see cref="ServerId"/> and activation change together. Every later operation on a live
    /// service, suspend, unsuspend, terminate, control-panel single sign-on, resolves its
    /// provisioning provider from <see cref="ServerId"/> and gives up silently when it is null,
    /// so a service that reached <see cref="ServiceStatus.Active"/> without a server can never be
    /// acted on again. Activating and assigning through two separate calls is what let that
    /// happen, which is why this is one method rather than a public setter.
    /// </remarks>
    /// <param name="serverId">Identifier of the server the hosting account now lives on.</param>
    /// <param name="provisioningRef">External reference from the provisioning provider.</param>
    /// <param name="billingAnchor">The moment the billing period starts from (UTC), passed on to <see cref="Activate"/>.</param>
    /// <param name="now">The current moment (UTC), read by the caller; stamped on the domain event.</param>
    /// <exception cref="DomainRuleException">
    /// Thrown with <see cref="ServicesErrors.ServiceNotPending"/> when the service is not in Pending status.
    /// </exception>
    public void ActivateOn(int serverId, string provisioningRef, DateTimeOffset billingAnchor, DateTimeOffset now)
    {
        // Activate first: it is the guarded half, and a refused activation must leave the
        // server assignment untouched.
        Activate(provisioningRef, billingAnchor, now);
        ServerId = serverId;
    }

    /// <summary>
    /// Records the hosting account details a pending service will be provisioned with. Domain,
    /// username and password change together, because the panel account is created from all three.
    /// </summary>
    /// <param name="domain">The account's primary domain.</param>
    /// <param name="username">The account's username on the panel.</param>
    /// <param name="password">The account's password on the panel.</param>
    /// <exception cref="DomainRuleException">Thrown when the service is not in Pending status.</exception>
    public void AssignHostingAccount(string domain, string username, string password)
    {
        if (Status is not ServiceStatus.Pending)
        {
            throw new DomainRuleException(
                ServicesErrors.ServiceNotPending,
                $"Cannot set up a service with status {Status}. Only Pending services can be set up.");
        }

        Domain = domain;
        Username = username;
        Password = password;
    }

    /// <summary>
    /// Suspends the service and raises <see cref="ClientServiceSuspendedEvent"/>
    /// and <see cref="ServiceSuspendedEvent"/>.
    /// </summary>
    /// <param name="now">The current moment (UTC), read by the caller; becomes <see cref="SuspendedAt"/> and is stamped on the domain events.</param>
    /// <param name="reason">Human-readable reason for the suspension.</param>
    /// <param name="hostingAlreadyUpdated">
    /// The caller has already suspended the hosting account itself (and answered its failure), so
    /// <see cref="ServiceSuspendedEvent"/>, the request for the panel to do it, is not raised
    /// and the account is not suspended a second time.
    /// </param>
    /// <param name="source">Why it is suspended; decides whether a later payment may lift it.</param>
    /// <exception cref="DomainRuleException">Thrown when the service is not in Active status.</exception>
    public void Suspend(DateTimeOffset now, string reason = "", bool hostingAlreadyUpdated = false, SuspensionSource source = Enums.SuspensionSource.Manual)
    {
        if (!CanSuspend)
        {
            throw new DomainRuleException(
                ServicesErrors.ServiceNotActive,
                $"Cannot suspend a service with status {Status}. Only Active services can be suspended.");
        }

        Status = ServiceStatus.Suspended;
        SuspendedAt = now;
        SuspensionSource = source;
        AddDomainEvent(new ClientServiceSuspendedEvent(Id, ClientId, now));
        if (!hostingAlreadyUpdated)
        {
            AddDomainEvent(new ServiceSuspendedEvent(Id, ClientId, reason, now));
        }
    }

    /// <summary>
    /// Terminates the service and raises <see cref="ClientServiceTerminatedEvent"/>
    /// and <see cref="ServiceTerminatedEvent"/>.
    /// </summary>
    /// <param name="now">The current moment (UTC), read by the caller; becomes <see cref="TerminatedAt"/> and is stamped on the domain events.</param>
    /// <param name="reason">Human-readable reason for the termination.</param>
    /// <param name="hostingAlreadyUpdated">
    /// The caller has already terminated the hosting account itself, so
    /// <see cref="ServiceTerminatedEvent"/> is not raised and the account is not terminated twice.
    /// </param>
    /// <exception cref="DomainRuleException">Thrown when the service is already terminated.</exception>
    public void Terminate(DateTimeOffset now, string reason = "", bool hostingAlreadyUpdated = false)
    {
        if (!CanTerminate)
        {
            throw new DomainRuleException(ServicesErrors.ServiceTerminateNotAllowed, "Service is already terminated.");
        }

        Status = ServiceStatus.Terminated;
        TerminatedAt = now;
        AddDomainEvent(new ClientServiceTerminatedEvent(Id, ClientId, now));
        if (!hostingAlreadyUpdated)
        {
            AddDomainEvent(new ServiceTerminatedEvent(Id, ClientId, reason, now));
        }
    }

    /// <summary>
    /// Re-activates a previously suspended service, restoring it to active status.
    /// </summary>
    /// <param name="now">The current moment (UTC), read by the caller; stamped on the domain event.</param>
    /// <param name="hostingAlreadyUpdated">
    /// The caller has already unsuspended the hosting account itself, so
    /// <see cref="Provisioning.Events.ServiceUnsuspendedEvent"/> is not raised and the account is not
    /// unsuspended twice.
    /// </param>
    /// <exception cref="DomainRuleException">Thrown when the service is not in Suspended status.</exception>
    public void Unsuspend(DateTimeOffset now, bool hostingAlreadyUpdated = false)
    {
        if (!CanUnsuspend)
        {
            throw new DomainRuleException(
                ServicesErrors.ServiceNotSuspended,
                $"Cannot unsuspend a service with status {Status}. Only Suspended services can be unsuspended.");
        }

        Status = ServiceStatus.Active;
        SuspendedAt = null;
        SuspensionSource = null;
        AddDomainEvent(new ClientServiceUnsuspendedEvent(Id, ClientId, now));
        if (!hostingAlreadyUpdated)
        {
            AddDomainEvent(new Provisioning.Events.ServiceUnsuspendedEvent(Id, ClientId, now));
        }
    }

    /// <summary>
    /// Records that the period ending at <paramref name="periodEnd"/> is paid: the renewal date
    /// becomes the later of its current value and <paramref name="periodEnd"/>. Never moves a date
    /// back, so a replayed payment or a date staff already pushed further is left alone.
    /// </summary>
    /// <param name="periodEnd">End of the paid period.</param>
    public void ExtendRenewalTo(DateTimeOffset periodEnd)
    {
        if (NextRenewalAt is null || periodEnd > NextRenewalAt)
        {
            NextRenewalAt = periodEnd;
        }
    }

    /// <summary>
    /// Replaces what the service is billed: cycle, quantity, both amounts, the next renewal date and the
    /// payment references, as one change.
    /// </summary>
    /// <param name="terms">The new billing terms.</param>
    public void ChangeBillingTerms(ServiceBillingTerms terms)
    {
        ArgumentNullException.ThrowIfNull(terms);

        BillingCycle = terms.BillingCycle;
        Quantity = terms.Quantity;
        FirstPaymentAmount = terms.FirstPaymentAmount;
        RecurringAmount = terms.RecurringAmount;
        NextRenewalAt = terms.NextRenewalAt;
        PaymentMethod = terms.PaymentMethod;
        PromotionCode = terms.PromotionCode;
        SubscriptionId = terms.SubscriptionId;
    }

    /// <summary>Moves the service to another product of the catalogue.</summary>
    /// <param name="productId">FK to the product the service is now of.</param>
    public void ChangeProduct(int productId) => ProductId = productId;

    /// <summary>
    /// Records the hosting account the service lives on as staff see it on the panel: server, domain,
    /// panel reference, dedicated address and credentials. Unlike <see cref="AssignHostingAccount"/>,
    /// which prepares a pending service for provisioning, this corrects the record of an account in
    /// any status and creates nothing on the panel.
    /// </summary>
    /// <param name="account">The hosting account details.</param>
    public void RecordHostingAccount(ServiceHostingAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        ServerId = account.ServerId;
        Domain = account.Domain;
        ProvisioningRef = account.ProvisioningRef;
        DedicatedIp = account.DedicatedIp;
        Username = account.Username;
        Password = account.Password;
    }

    /// <summary>
    /// Sets how the billing runs treat the service: whether they look after it at all, whether the
    /// overdue suspension is held off (and until when), and whether the service ends with its current
    /// cycle (and why).
    /// </summary>
    /// <param name="autoRenew">Whether the billing runs look after the service; see <see cref="AutoRenew"/>.</param>
    /// <param name="overrideAutoSuspend">Whether the overdue suspension is held off.</param>
    /// <param name="suspendUntil">Date the hold lasts until, or <see langword="null"/> for no end; only with <paramref name="overrideAutoSuspend"/>.</param>
    /// <param name="autoTerminateEndOfCycle">Whether the service is terminated when its current cycle ends.</param>
    /// <param name="autoTerminateReason">Why it ends, or <see langword="null"/>; only with <paramref name="autoTerminateEndOfCycle"/>.</param>
    /// <exception cref="DomainRuleException">
    /// Thrown with <see cref="ServicesErrors.ServiceAutomationInconsistent"/> when a hold date is given
    /// without the hold, or a termination reason without the termination.
    /// </exception>
    public void ConfigureAutomation(
        bool autoRenew, bool overrideAutoSuspend, DateTimeOffset? suspendUntil, bool autoTerminateEndOfCycle, string? autoTerminateReason)
    {
        if ((!overrideAutoSuspend && suspendUntil is not null) || (!autoTerminateEndOfCycle && autoTerminateReason is not null))
        {
            throw new DomainRuleException(
                ServicesErrors.ServiceAutomationInconsistent,
                "A hold date needs the suspension hold, and a termination reason needs the end-of-cycle termination.");
        }

        AutoRenew = autoRenew;
        OverrideAutoSuspend = overrideAutoSuspend;
        SuspendUntil = suspendUntil;
        AutoTerminateEndOfCycle = autoTerminateEndOfCycle;
        AutoTerminateReason = autoTerminateReason;
    }

    /// <summary>Replaces the internal notes staff keep on the service.</summary>
    /// <param name="notes">The notes, or <see langword="null"/> to clear them.</param>
    public void UpdateAdminNotes(string? notes) => AdminNotes = notes;

    /// <summary>
    /// Corrects the recorded termination date of a terminated service, for a termination that really
    /// happened on another day than the one it was recorded on.
    /// </summary>
    /// <param name="terminatedAt">The moment the service really ended (UTC).</param>
    /// <exception cref="DomainRuleException">
    /// Thrown with <see cref="ServicesErrors.ServiceNotTerminated"/> when the service is not terminated.
    /// </exception>
    public void CorrectTerminationDate(DateTimeOffset terminatedAt)
    {
        if (Status is not ServiceStatus.Terminated)
        {
            throw new DomainRuleException(
                ServicesErrors.ServiceNotTerminated,
                $"Cannot correct the termination date of a service with status {Status}. Only Terminated services have one.");
        }

        TerminatedAt = terminatedAt;
    }
}
