using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Provisioning.Events;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Domain.Services.Events;

namespace Innovayse.Hostpanel.Domain.Tests.Services;

/// <summary>Unit tests for the <see cref="ClientService"/> aggregate.</summary>
public class ClientServiceTests
{
    /// <summary>The fixed moment every domain method in these tests is handed as "now".</summary>
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Verifies that <see cref="ClientService.Create"/> sets Pending status and raises the created event.</summary>
    [Fact]
    public void Create_SetsPendingStatus_AndRaisesEvent()
    {
        var svc = ClientService.Create(clientId: 1, productId: 2, billingCycle: BillingCycle.Monthly, now: Now);

        Assert.Equal(1, svc.ClientId);
        Assert.Equal(2, svc.ProductId);
        Assert.Equal(BillingCycle.Monthly, svc.BillingCycle);
        Assert.Equal(ServiceStatus.Pending, svc.Status);
        Assert.Single(svc.DomainEvents);
        var created = Assert.IsType<ClientServiceCreatedEvent>(svc.DomainEvents.Single());
        Assert.Equal(Now, created.OccurredAt);
    }

    /// <summary>Verifies that <see cref="ClientService.Activate"/> transitions status to Active and stores the provisioning ref.</summary>
    [Fact]
    public void Activate_ChangesStatusToActive()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        var activatedAt = Now.AddDays(1);
        svc.Activate("panel-acc-123", Now, activatedAt);
        Assert.Equal(ServiceStatus.Active, svc.Status);
        Assert.Equal("panel-acc-123", svc.ProvisioningRef);
        Assert.Equal(activatedAt, svc.DomainEvents.OfType<ServiceProvisionedEvent>().Single().OccurredAt);
    }

    /// <summary>Verifies that <see cref="ClientService.Suspend"/> transitions status to Suspended and raises the suspended event.</summary>
    [Fact]
    public void Suspend_ChangeStatusToSuspended_AndRaisesEvent()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        svc.Activate("ref", Now, Now);
        svc.Suspend(Now);
        Assert.Equal(ServiceStatus.Suspended, svc.Status);
        Assert.All(svc.DomainEvents, e => Assert.Equal(Now, e.OccurredAt));
        Assert.Contains(svc.DomainEvents, e => e is ClientServiceSuspendedEvent);
    }

    /// <summary>
    /// A caller that already updated the hosting account raises no event asking the panel to do it
    /// again, for suspend, terminate and unsuspend alike; the platform-level event still fires.
    /// </summary>
    [Fact]
    public void HostingAlreadyUpdated_RaisesNoProviderEvent()
    {
        var suspended = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        suspended.Activate("ref", Now, Now);
        suspended.Suspend(Now, "overdue", hostingAlreadyUpdated: true);
        Assert.Contains(suspended.DomainEvents, e => e is ClientServiceSuspendedEvent);
        Assert.DoesNotContain(suspended.DomainEvents, e => e is ServiceSuspendedEvent);

        suspended.ClearDomainEvents();
        suspended.Unsuspend(Now, hostingAlreadyUpdated: true);
        Assert.Contains(suspended.DomainEvents, e => e is ClientServiceUnsuspendedEvent);
        Assert.DoesNotContain(suspended.DomainEvents, e => e is ServiceUnsuspendedEvent);

        var terminated = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        terminated.Activate("ref", Now, Now);
        terminated.Terminate(Now, "gone", hostingAlreadyUpdated: true);
        Assert.Contains(terminated.DomainEvents, e => e is ClientServiceTerminatedEvent);
        Assert.DoesNotContain(terminated.DomainEvents, e => e is ServiceTerminatedEvent);
    }

    /// <summary>Verifies that <see cref="ClientService.Terminate"/> transitions status to Terminated and raises the terminated event.</summary>
    [Fact]
    public void Terminate_ChangesStatusToTerminated_AndRaisesEvent()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        svc.Activate("ref", Now, Now);
        svc.Terminate(Now);
        Assert.Equal(ServiceStatus.Terminated, svc.Status);
        Assert.Equal(Now, svc.TerminatedAt);
        Assert.All(svc.DomainEvents, e => Assert.Equal(Now, e.OccurredAt));
        Assert.Contains(svc.DomainEvents, e => e is ClientServiceTerminatedEvent);
    }

    /// <summary>Verifies that <see cref="ClientService.Activate"/> sets NextRenewalAt for monthly and annual billing cycles.</summary>
    [Fact]
    public void Activate_SetsNextRenewalAt_ForMonthlyAndAnnual()
    {
        var monthlySvc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        monthlySvc.Activate("ref-m", Now, Now);
        Assert.NotNull(monthlySvc.NextRenewalAt);
        Assert.True(monthlySvc.NextRenewalAt > Now);

        var annualSvc = ClientService.Create(1, 2, BillingCycle.Annual, Now);
        annualSvc.Activate("ref-a", Now, Now);
        Assert.NotNull(annualSvc.NextRenewalAt);
        Assert.True(annualSvc.NextRenewalAt > monthlySvc.NextRenewalAt);
    }

    /// <summary>Verifies that <see cref="ClientService.Suspend"/> sets SuspendedAt timestamp.</summary>
    [Fact]
    public void Suspend_SetsSuspendedAt()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        svc.Activate("ref", Now, Now);

        svc.Suspend(Now);

        Assert.Equal(Now, svc.SuspendedAt);
    }

    /// <summary>Verifies that <see cref="ClientService.Unsuspend"/> clears SuspendedAt.</summary>
    [Fact]
    public void Unsuspend_ClearsSuspendedAt()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        svc.Activate("ref", Now, Now);
        svc.Suspend(Now);
        Assert.NotNull(svc.SuspendedAt);

        svc.Unsuspend(Now);

        Assert.Null(svc.SuspendedAt);
        Assert.Equal(ServiceStatus.Active, svc.Status);
    }

    /// <summary>Suspend on a Pending service throws <see cref="DomainRuleException"/> with <c>SERVICE_NOT_ACTIVE</c>.</summary>
    [Fact]
    public void Suspend_WhenNotActive_ThrowsWithServiceNotActive()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);

        var ex = Assert.Throws<DomainRuleException>(() => svc.Suspend(Now));

        Assert.Equal(ServicesErrors.ServiceNotActive, ex.Code);
    }

    /// <summary>Terminate on an already-terminated service throws <see cref="DomainRuleException"/> with <c>SERVICE_TERMINATE_NOT_ALLOWED</c>.</summary>
    [Fact]
    public void Terminate_WhenAlreadyTerminated_ThrowsWithServiceTerminateNotAllowed()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        svc.Activate("ref", Now, Now);
        svc.Terminate(Now);

        var ex = Assert.Throws<DomainRuleException>(() => svc.Terminate(Now));

        Assert.Equal(ServicesErrors.ServiceTerminateNotAllowed, ex.Code);
    }

    /// <summary>Unsuspend on an Active service throws <see cref="DomainRuleException"/> with <c>SERVICE_NOT_SUSPENDED</c>.</summary>
    [Fact]
    public void Unsuspend_WhenNotSuspended_ThrowsWithServiceNotSuspended()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        svc.Activate("ref", Now, Now);

        var ex = Assert.Throws<DomainRuleException>(() => svc.Unsuspend(Now));

        Assert.Equal(ServicesErrors.ServiceNotSuspended, ex.Code);
    }

    /// <summary>Verifies that <see cref="ClientService.Backdate"/> moves the creation date and keeps the status.</summary>
    [Fact]
    public void Backdate_MovesTheCreationDateOnly()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        var ordered = new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero);

        svc.Backdate(ordered);

        Assert.Equal(ordered, svc.CreatedAt);
        Assert.Equal(ServiceStatus.Pending, svc.Status);
    }

    /// <summary>Verifies that <see cref="ClientService.CreateOrdered"/> stores the order's terms on a pending service.</summary>
    [Fact]
    public void CreateOrdered_StoresTheOrderTerms()
    {
        var svc = ClientService.CreateOrdered(1, 2, BillingCycle.Monthly, "example.com", 10m, 8m, "BankTransfer", null, Now);

        Assert.Equal(ServiceStatus.Pending, svc.Status);
        Assert.Equal("example.com", svc.Domain);
        Assert.Equal(10m, svc.FirstPaymentAmount);
        Assert.Equal(8m, svc.RecurringAmount);
        Assert.Equal("BankTransfer", svc.PaymentMethod);
        Assert.IsType<ClientServiceCreatedEvent>(svc.DomainEvents.Single());
    }

    /// <summary>
    /// Verifies that <see cref="ClientService.CreateOrdered"/> stores the order line the service is created for, and
    /// that a service not ordered through a line has none.
    /// </summary>
    [Fact]
    public void CreateOrdered_StoresTheOrderLineItWasCreatedFor()
    {
        var forLine = ClientService.CreateOrdered(1, 2, BillingCycle.Monthly, null, 10m, 8m, null, null, Now, orderItemId: 41);
        var plain = ClientService.CreateOrdered(1, 2, BillingCycle.Monthly, null, 10m, 8m, null, null, Now);

        Assert.Equal(41, forLine.OrderItemId);
        Assert.Null(plain.OrderItemId);
    }

    /// <summary>Verifies that <see cref="ClientService.AssignHostingAccount"/> stores the three details on a pending service.</summary>
    [Fact]
    public void AssignHostingAccount_OnPending_StoresTheDetails()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);

        svc.AssignHostingAccount("example.com", "user1", "secret123");

        Assert.Equal("example.com", svc.Domain);
        Assert.Equal("user1", svc.Username);
        Assert.Equal("secret123", svc.Password);
    }

    /// <summary>Verifies that <see cref="ClientService.AssignHostingAccount"/> refuses a service that is no longer pending.</summary>
    [Fact]
    public void AssignHostingAccount_WhenNotPending_ThrowsWithServiceNotPending()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        svc.Activate("acct", Now, Now);

        var ex = Assert.Throws<DomainRuleException>(() => svc.AssignHostingAccount("example.com", "user1", "secret123"));

        Assert.Equal(ServicesErrors.ServiceNotPending, ex.Code);
    }

    /// <summary>
    /// The full status table of <see cref="ClientService.CanChangeStatusTo"/>: from each status (rows)
    /// to each target, whether the move is allowed.
    /// </summary>
    /// <param name="from">The status the service is in.</param>
    /// <param name="target">The status asked for.</param>
    /// <param name="allowed">Whether the move is allowed.</param>
    [Theory]
    [InlineData(ServiceStatus.Pending, ServiceStatus.Pending, true)]
    [InlineData(ServiceStatus.Pending, ServiceStatus.Active, false)]
    [InlineData(ServiceStatus.Pending, ServiceStatus.Suspended, false)]
    [InlineData(ServiceStatus.Pending, ServiceStatus.Terminated, true)]
    [InlineData(ServiceStatus.Active, ServiceStatus.Pending, false)]
    [InlineData(ServiceStatus.Active, ServiceStatus.Active, true)]
    [InlineData(ServiceStatus.Active, ServiceStatus.Suspended, true)]
    [InlineData(ServiceStatus.Active, ServiceStatus.Terminated, true)]
    [InlineData(ServiceStatus.Suspended, ServiceStatus.Pending, false)]
    [InlineData(ServiceStatus.Suspended, ServiceStatus.Active, true)]
    [InlineData(ServiceStatus.Suspended, ServiceStatus.Suspended, true)]
    [InlineData(ServiceStatus.Suspended, ServiceStatus.Terminated, true)]
    [InlineData(ServiceStatus.Terminated, ServiceStatus.Pending, false)]
    [InlineData(ServiceStatus.Terminated, ServiceStatus.Active, false)]
    [InlineData(ServiceStatus.Terminated, ServiceStatus.Suspended, false)]
    [InlineData(ServiceStatus.Terminated, ServiceStatus.Terminated, true)]
    public void CanChangeStatusTo_FollowsTheStatusTable(ServiceStatus from, ServiceStatus target, bool allowed)
    {
        var svc = InStatus(from);

        Assert.Equal(allowed, svc.CanChangeStatusTo(target));
    }

    /// <summary>
    /// <see cref="ClientService.CanSuspend"/>, <see cref="ClientService.CanUnsuspend"/> and
    /// <see cref="ClientService.CanTerminate"/> agree with what the transitions themselves accept.
    /// </summary>
    /// <param name="from">The status the service is in.</param>
    /// <param name="canSuspend">Whether it may be suspended.</param>
    /// <param name="canUnsuspend">Whether it may be unsuspended.</param>
    /// <param name="canTerminate">Whether it may be terminated.</param>
    [Theory]
    [InlineData(ServiceStatus.Pending, false, false, true)]
    [InlineData(ServiceStatus.Active, true, false, true)]
    [InlineData(ServiceStatus.Suspended, false, true, true)]
    [InlineData(ServiceStatus.Terminated, false, false, false)]
    public void CanTransition_MatchesTheTransitionGuards(ServiceStatus from, bool canSuspend, bool canUnsuspend, bool canTerminate)
    {
        var svc = InStatus(from);

        Assert.Equal(canSuspend, svc.CanSuspend);
        Assert.Equal(canUnsuspend, svc.CanUnsuspend);
        Assert.Equal(canTerminate, svc.CanTerminate);
        Assert.Equal(canSuspend, Record.Exception(() => InStatus(from).Suspend(Now)) is null);
        Assert.Equal(canUnsuspend, Record.Exception(() => InStatus(from).Unsuspend(Now)) is null);
        Assert.Equal(canTerminate, Record.Exception(() => InStatus(from).Terminate(Now)) is null);
    }

    /// <summary>
    /// Activating a service that is no longer Pending throws <see cref="DomainRuleException"/> with
    /// <c>SERVICE_NOT_PENDING</c>, so the caller is answered 400 rather than 500.
    /// </summary>
    [Fact]
    public void Activate_WhenNotPending_ThrowsWithServiceNotPending()
    {
        var svc = InStatus(ServiceStatus.Active);

        var ex = Assert.Throws<DomainRuleException>(() => svc.Activate("again", Now, Now));

        Assert.Equal(ServicesErrors.ServiceNotPending, ex.Code);
    }

    /// <summary>Builds a service in <paramref name="status"/> through the real transitions.</summary>
    /// <param name="status">The status wanted.</param>
    /// <returns>The service.</returns>
    private static ClientService InStatus(ServiceStatus status)
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        if (status is ServiceStatus.Pending)
        {
            return svc;
        }

        if (status is ServiceStatus.Terminated)
        {
            svc.Terminate(Now, hostingAlreadyUpdated: true);
            return svc;
        }

        svc.Activate("acct", Now, Now);
        if (status is ServiceStatus.Suspended)
        {
            svc.Suspend(Now, hostingAlreadyUpdated: true);
        }

        return svc;
    }

    /// <summary>Verifies that <see cref="ClientService.ChangeBillingTerms"/> replaces every billing term at once.</summary>
    [Fact]
    public void ChangeBillingTerms_ReplacesEveryTerm()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        var renewal = new DateTimeOffset(2031, 3, 4, 0, 0, 0, TimeSpan.Zero);

        svc.ChangeBillingTerms(new ServiceBillingTerms(
            BillingCycle.Annual, 3, 10m, 100m, renewal, "card", "SPRING", "sub-1"));

        Assert.Equal(BillingCycle.Annual, svc.BillingCycle);
        Assert.Equal(3, svc.Quantity);
        Assert.Equal(10m, svc.FirstPaymentAmount);
        Assert.Equal(100m, svc.RecurringAmount);
        Assert.Equal(renewal, svc.NextRenewalAt);
        Assert.Equal("card", svc.PaymentMethod);
        Assert.Equal("SPRING", svc.PromotionCode);
        Assert.Equal("sub-1", svc.SubscriptionId);
    }

    /// <summary>Verifies that <see cref="ClientService.ChangeProduct"/> moves the service to the named product.</summary>
    [Fact]
    public void ChangeProduct_SetsTheProduct()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);

        svc.ChangeProduct(7);

        Assert.Equal(7, svc.ProductId);
    }

    /// <summary>
    /// Verifies that <see cref="ClientService.RecordHostingAccount"/> stores the account details on a
    /// service that is already live, unlike <see cref="ClientService.AssignHostingAccount"/>.
    /// </summary>
    [Fact]
    public void RecordHostingAccount_OnAnActiveService_StoresEveryDetail()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        svc.Activate("acct", Now, Now);

        svc.RecordHostingAccount(new ServiceHostingAccount(4, "site.example", "acct2", "198.51.100.7", "siteuser", "pw"));

        Assert.Equal(4, svc.ServerId);
        Assert.Equal("site.example", svc.Domain);
        Assert.Equal("acct2", svc.ProvisioningRef);
        Assert.Equal("198.51.100.7", svc.DedicatedIp);
        Assert.Equal("siteuser", svc.Username);
        Assert.Equal("pw", svc.Password);
        Assert.Equal(ServiceStatus.Active, svc.Status);
    }

    /// <summary>
    /// Verifies that <see cref="ClientService.ConfigureAutomation"/> stores manual billing, the hold and
    /// the end-of-cycle termination.
    /// </summary>
    [Fact]
    public void ConfigureAutomation_WithEverySwitch_StoresTheirValues()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        var holdUntil = new DateTimeOffset(2031, 5, 6, 0, 0, 0, TimeSpan.Zero);

        svc.ConfigureAutomation(false, true, holdUntil, true, "Moving away");

        Assert.False(svc.AutoRenew);
        Assert.True(svc.OverrideAutoSuspend);
        Assert.Equal(holdUntil, svc.SuspendUntil);
        Assert.True(svc.AutoTerminateEndOfCycle);
        Assert.Equal("Moving away", svc.AutoTerminateReason);
    }

    /// <summary>
    /// Verifies that <see cref="ClientService.ConfigureAutomation"/> refuses a hold date without the hold
    /// and a reason without the termination, and changes nothing when it does.
    /// </summary>
    [Fact]
    public void ConfigureAutomation_WithAValueButNotItsSwitch_ThrowsAutomationInconsistent()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);

        var withHoldDate = Assert.Throws<DomainRuleException>(
            () => svc.ConfigureAutomation(true, false, Now, false, null));
        var withReason = Assert.Throws<DomainRuleException>(
            () => svc.ConfigureAutomation(true, false, null, false, "Moving away"));

        Assert.Equal(ServicesErrors.ServiceAutomationInconsistent, withHoldDate.Code);
        Assert.Equal(ServicesErrors.ServiceAutomationInconsistent, withReason.Code);
        Assert.Null(svc.SuspendUntil);
        Assert.Null(svc.AutoTerminateReason);
    }

    /// <summary>Verifies that <see cref="ClientService.UpdateAdminNotes"/> replaces and clears the notes.</summary>
    [Fact]
    public void UpdateAdminNotes_ReplacesAndClears()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);

        svc.UpdateAdminNotes("Call first.");
        Assert.Equal("Call first.", svc.AdminNotes);

        svc.UpdateAdminNotes(null);
        Assert.Null(svc.AdminNotes);
    }

    /// <summary>Verifies that <see cref="ClientService.CorrectTerminationDate"/> moves the date of a terminated service.</summary>
    [Fact]
    public void CorrectTerminationDate_OnATerminatedService_StoresTheDate()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        svc.Activate("acct", Now, Now);
        svc.Terminate(Now, hostingAlreadyUpdated: true);
        var endedAt = new DateTimeOffset(2030, 1, 15, 0, 0, 0, TimeSpan.Zero);

        svc.CorrectTerminationDate(endedAt);

        Assert.Equal(endedAt, svc.TerminatedAt);
    }

    /// <summary>Verifies that <see cref="ClientService.CorrectTerminationDate"/> refuses a service that is not terminated.</summary>
    [Fact]
    public void CorrectTerminationDate_OnAnActiveService_ThrowsNotTerminated()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        svc.Activate("acct", Now, Now);

        var ex = Assert.Throws<DomainRuleException>(() => svc.CorrectTerminationDate(Now));

        Assert.Equal(ServicesErrors.ServiceNotTerminated, ex.Code);
        Assert.Null(svc.TerminatedAt);
    }

    /// <summary>
    /// Verifies that <see cref="ClientService.Activate"/> anchors the first period on the given
    /// moment, not on the order date or the moment stamped on the event.
    /// </summary>
    [Fact]
    public void Activate_AnchorsTheFirstPeriodOnTheActivationMoment()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        var activatedAt = new DateTimeOffset(2026, 4, 20, 9, 30, 0, TimeSpan.Zero);

        svc.Activate("ref", activatedAt, Now);

        Assert.Equal(new DateTimeOffset(2026, 5, 20, 9, 30, 0, TimeSpan.Zero), svc.NextRenewalAt);
    }

    /// <summary>Verifies that an annual service activated on a given moment renews exactly a year later.</summary>
    [Fact]
    public void Activate_Annual_RenewsOneYearAfterTheActivationMoment()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Annual, Now);
        var activatedAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

        svc.Activate("ref", activatedAt, Now);

        Assert.Equal(new DateTimeOffset(2027, 7, 1, 0, 0, 0, TimeSpan.Zero), svc.NextRenewalAt);
    }

    /// <summary>
    /// Verifies that <see cref="ClientService.Activate"/> keeps a renewal date staff already set
    /// later than the first period's end, the same rule as <see cref="ClientService.ExtendRenewalTo"/>.
    /// </summary>
    [Fact]
    public void Activate_NeverLowersALaterRenewalDate()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        var later = new DateTimeOffset(2027, 1, 15, 0, 0, 0, TimeSpan.Zero);
        svc.ChangeBillingTerms(new ServiceBillingTerms(
            BillingCycle.Monthly, 1, 5m, 5m, later, null, null, null));

        svc.Activate("ref", new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero), Now);

        Assert.Equal(later, svc.NextRenewalAt);
    }

    /// <summary>
    /// Verifies that <see cref="ClientService.Activate"/> replaces an earlier renewal date with the
    /// end of the first period.
    /// </summary>
    [Fact]
    public void Activate_MovesAnEarlierRenewalDateToThePeriodEnd()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        svc.ChangeBillingTerms(new ServiceBillingTerms(
            BillingCycle.Monthly, 1, 5m, 5m, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), null, null, null));

        svc.Activate("ref", new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero), Now);

        Assert.Equal(new DateTimeOffset(2026, 5, 20, 0, 0, 0, TimeSpan.Zero), svc.NextRenewalAt);
    }

    /// <summary>
    /// Verifies that a service needs its first invoice paid only when the first payment is not free
    /// and the service names its first invoice; one without an invoice keeps the earlier behaviour.
    /// </summary>
    [Fact]
    public void RequiresPaidFirstInvoice_OnlyWhenTheFirstPaymentIsNotFreeAndHasAnInvoice()
    {
        var paid = ClientService.CreateOrdered(1, 2, BillingCycle.Monthly, null, 10m, 10m, null, 7, Now);
        var free = ClientService.CreateOrdered(1, 2, BillingCycle.Monthly, null, 0m, 10m, null, null, Now);
        var withoutInvoice = ClientService.CreateOrdered(1, 2, BillingCycle.Monthly, null, 10m, 10m, null, null, Now);

        Assert.True(paid.RequiresPaidFirstInvoice);
        Assert.Equal(7, paid.FirstInvoiceId);
        Assert.False(free.RequiresPaidFirstInvoice);
        Assert.Null(free.FirstInvoiceId);
        Assert.False(withoutInvoice.RequiresPaidFirstInvoice);
    }

    /// <summary>
    /// <see cref="ClientService.Activate"/> counts the first period from the billing anchor, not from
    /// the moment it runs: an anchor two months back renews exactly one cycle after the anchor, and
    /// the event carries the moment of activation.
    /// </summary>
    [Fact]
    public void Activate_WithAnAnchorBeforeNow_RenewsOneCycleAfterTheAnchor()
    {
        var anchor = new DateTimeOffset(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);
        var monthly = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        var annual = ClientService.Create(1, 2, BillingCycle.Annual, Now);

        monthly.Activate("ref-m", anchor, Now);
        annual.Activate("ref-a", anchor, Now);

        Assert.Equal(new DateTimeOffset(2026, 2, 15, 9, 30, 0, TimeSpan.Zero), monthly.NextRenewalAt);
        Assert.Equal(new DateTimeOffset(2027, 1, 15, 9, 30, 0, TimeSpan.Zero), annual.NextRenewalAt);
        Assert.Equal(Now, monthly.DomainEvents.OfType<ServiceProvisionedEvent>().Single().OccurredAt);
    }

    /// <summary><see cref="ClientService.ExtendRenewalTo"/> moves the date forward and never back.</summary>
    [Fact]
    public void ExtendRenewalTo_NeverMovesTheDateBack()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        svc.Activate("ref", Now, Now);
        var later = Now.AddMonths(3);

        svc.ExtendRenewalTo(later);
        svc.ExtendRenewalTo(Now.AddMonths(2));

        Assert.Equal(later, svc.NextRenewalAt);
    }

    /// <summary><see cref="ClientService.Unsuspend"/> clears why the service was suspended.</summary>
    [Fact]
    public void Unsuspend_ClearsSuspensionSource()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        svc.Activate("ref", Now, Now);
        svc.Suspend(Now, source: SuspensionSource.Overdue);
        Assert.Equal(SuspensionSource.Overdue, svc.SuspensionSource);

        svc.Unsuspend(Now);

        Assert.Null(svc.SuspensionSource);
    }

    /// <summary>
    /// A refused <see cref="ClientService.ActivateOn"/> throws <see cref="DomainRuleException"/> with
    /// <c>SERVICE_NOT_PENDING</c> and leaves the server assignment as it was.
    /// </summary>
    [Fact]
    public void ActivateOn_WhenRefused_KeepsTheServer()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        svc.ActivateOn(7, "ref", Now, Now);

        var ex = Assert.Throws<DomainRuleException>(() => svc.ActivateOn(9, "other", Now, Now));

        Assert.Equal(ServicesErrors.ServiceNotPending, ex.Code);

        Assert.Equal(7, svc.ServerId);
        Assert.Equal("ref", svc.ProvisioningRef);
    }

    /// <summary>
    /// <see cref="ClientService.Import"/> lands a suspended service in its state with its source,
    /// dates, server and panel reference, and raises no event: no welcome mail and no panel call.
    /// </summary>
    [Fact]
    public void Import_Suspended_SetsTheStateWithoutEvents()
    {
        var registered = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var svc = ClientService.Import(new ServiceImport(
            1, 2, BillingCycle.Monthly, ServiceStatus.Suspended, registered, Now, SuspensionSource.Manual,
            new ServiceHostingAccount(5, "example.test", "webuser", null, "webuser", null)));

        Assert.Empty(svc.DomainEvents);
        Assert.Equal(ServiceStatus.Suspended, svc.Status);
        Assert.Equal(SuspensionSource.Manual, svc.SuspensionSource);
        Assert.Equal(Now, svc.SuspendedAt);
        Assert.Equal(registered, svc.CreatedAt);
        Assert.Equal(new DateTimeOffset(2025, 7, 1, 0, 0, 0, TimeSpan.Zero), svc.NextRenewalAt);
        Assert.Equal(5, svc.ServerId);
        Assert.Equal("webuser", svc.ProvisioningRef);
        Assert.Null(svc.TerminatedAt);
    }

    /// <summary>
    /// An imported terminated service carries its termination date and no suspension source; an
    /// imported pending one has no renewal date yet. Neither raises an event.
    /// </summary>
    [Fact]
    public void Import_TerminatedAndPending_SetTheirOwnDates()
    {
        var registered = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var ended = new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero);
        var account = new ServiceHostingAccount(null, null, null, null, null, null);

        var terminated = ClientService.Import(new ServiceImport(
            1, 2, BillingCycle.Monthly, ServiceStatus.Terminated, registered, ended, SuspensionSource.Other, account));
        var pending = ClientService.Import(new ServiceImport(
            1, 2, BillingCycle.Monthly, ServiceStatus.Pending, registered, Now, SuspensionSource.Other, account));

        Assert.Empty(terminated.DomainEvents);
        Assert.Equal(ended, terminated.TerminatedAt);
        Assert.Null(terminated.SuspensionSource);
        Assert.Null(terminated.SuspendedAt);
        Assert.Empty(pending.DomainEvents);
        Assert.Null(pending.NextRenewalAt);
    }

    /// <summary>
    /// Verifies that <see cref="ClientService.SetAutoRenew"/> flips only the billing switch and leaves the hold and
    /// the end-of-cycle termination as they were.
    /// </summary>
    [Fact]
    public void SetAutoRenew_FlipsOnlyTheBillingSwitch()
    {
        var svc = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        var holdUntil = new DateTimeOffset(2031, 5, 6, 0, 0, 0, TimeSpan.Zero);
        svc.ConfigureAutomation(true, true, holdUntil, true, "Moving away");

        svc.SetAutoRenew(false);

        Assert.False(svc.AutoRenew);
        Assert.True(svc.OverrideAutoSuspend);
        Assert.Equal(holdUntil, svc.SuspendUntil);
        Assert.True(svc.AutoTerminateEndOfCycle);
        Assert.Equal("Moving away", svc.AutoTerminateReason);

        svc.SetAutoRenew(true);

        Assert.True(svc.AutoRenew);
    }
}
