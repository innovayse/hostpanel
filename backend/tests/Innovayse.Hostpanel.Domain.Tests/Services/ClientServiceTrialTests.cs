using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Domain.Services.Events;

namespace Innovayse.Hostpanel.Domain.Tests.Services;

/// <summary>The free-trial fields of <see cref="ClientService"/>: starting a trial and how activation treats it.</summary>
public sealed class ClientServiceTrialTests
{
    /// <summary>The fixed moment every service in these tests is ordered at.</summary>
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The trial rules used: 14 days, auto-convert, reminders 3 and 1, grace 3.</summary>
    private static readonly ProductTrial Rules = new(true, 14, true, new[] { 3, 1 }, 3);

    /// <summary>Starting a trial sets Active, the end date and a copy of the product's rules.</summary>
    [Fact]
    public void StartTrial_SetsActiveStatusEndDateAndTheProductsRules()
    {
        var service = ClientService.Create(1, 2, BillingCycle.Monthly, Now);

        service.StartTrial(Rules, Now);

        service.TrialStatus.Should().Be(TrialStatus.Active);
        service.TrialEndsAt.Should().Be(Now.AddDays(14));
        service.TrialAutoConvert.Should().BeTrue();
        service.TrialGracePeriodDays.Should().Be(3);
        service.TrialReminderDays.Should().Be("3,1");
    }

    /// <summary>Starting a trial refuses a service that is not Pending or already is a trial.</summary>
    [Fact]
    public void StartTrial_OnANonPendingOrAlreadyTrialService_Throws()
    {
        var active = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        active.ActivateDigital(Now);
        var trial = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        trial.StartTrial(Rules, Now);

        var onActive = () => active.StartTrial(Rules, Now);
        var onTrial = () => trial.StartTrial(Rules, Now);

        onActive.Should().Throw<DomainRuleException>();
        onTrial.Should().Throw<DomainRuleException>();
    }

    /// <summary>Digital activation of a trial renews at the trial end.</summary>
    [Fact]
    public void ActivateDigital_OfATrialSetsNextRenewalToTheTrialEnd()
    {
        var service = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        service.StartTrial(Rules, Now);

        service.ActivateDigital(Now);

        service.NextRenewalAt.Should().Be(service.TrialEndsAt);
    }

    /// <summary>Provisioned activation of a trial renews at the trial end.</summary>
    [Fact]
    public void Activate_OfATrialSetsNextRenewalToTheTrialEnd()
    {
        var service = ClientService.Create(1, 2, BillingCycle.Annual, Now);
        service.StartTrial(Rules, Now);

        service.Activate("acct", Now, Now);

        service.NextRenewalAt.Should().Be(service.TrialEndsAt);
    }

    /// <summary>An ordinary service has no trial and renews one cycle after the anchor.</summary>
    [Fact]
    public void Activate_OfAnOrdinaryServiceIsUnchanged()
    {
        var service = ClientService.Create(1, 2, BillingCycle.Monthly, Now);

        service.Activate("acct", Now, Now);

        service.TrialStatus.Should().BeNull();
        service.NextRenewalAt.Should().Be(Now.AddMonths(1));
    }

    /// <summary>Ordering with trial rules starts the trial.</summary>
    [Fact]
    public void CreateOrdered_WithATrialStartsIt()
    {
        var service = ClientService.CreateOrdered(1, 2, BillingCycle.Monthly, null, 0m, 10m, null, null, Now, trial: Rules);

        service.TrialStatus.Should().Be(TrialStatus.Active);
        service.TrialEndsAt.Should().Be(Now.AddDays(14));
        service.TrialAutoConvert.Should().BeTrue();
        service.TrialGracePeriodDays.Should().Be(3);
        service.TrialReminderDays.Should().Be("3,1");
    }

    /// <summary>Ordering without trial rules leaves an ordinary pending service.</summary>
    [Fact]
    public void CreateOrdered_WithoutATrialIsOrdinary()
    {
        var service = ClientService.CreateOrdered(1, 2, BillingCycle.Monthly, null, 5m, 10m, null, null, Now, trial: null);

        service.TrialStatus.Should().BeNull();
        service.TrialEndsAt.Should().BeNull();
        service.Status.Should().Be(ServiceStatus.Pending);
    }

    /// <summary>A service that is not a trial has no days left.</summary>
    [Fact]
    public void TrialDaysLeft_OfAnOrdinaryService_IsNull()
    {
        var service = ClientService.Create(1, 2, BillingCycle.Monthly, Now);

        service.TrialDaysLeft(Now).Should().BeNull();
    }

    /// <summary>A trial counts the whole days to its end, rounded up, so a trial ending later today shows one.</summary>
    /// <param name="hoursAfterStart">How long after the start the question is asked.</param>
    /// <param name="expected">The days left.</param>
    [Theory]
    [InlineData(0, 14)]
    [InlineData(1, 14)]
    [InlineData(24, 13)]
    [InlineData(25, 13)]
    [InlineData(313, 1)]
    [InlineData(335, 1)]
    public void TrialDaysLeft_OfAnActiveTrial_RoundsUp(int hoursAfterStart, int expected)
    {
        var service = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        service.StartTrial(Rules, Now);

        service.TrialDaysLeft(Now.AddHours(hoursAfterStart)).Should().Be(expected);
    }

    /// <summary>Once the end has passed the days left are zero, never negative.</summary>
    [Fact]
    public void TrialDaysLeft_AfterTheEnd_IsZero()
    {
        var service = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        service.StartTrial(Rules, Now);

        service.TrialDaysLeft(Now.AddDays(14)).Should().Be(0);
        service.TrialDaysLeft(Now.AddDays(30)).Should().Be(0);
    }

    /// <summary>Builds an Active trial service started at <see cref="Now"/>.</summary>
    /// <returns>The activated trial.</returns>
    private static ClientService ActiveTrial()
    {
        var service = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        service.StartTrial(Rules, Now);
        service.ActivateDigital(Now);
        service.ClearDomainEvents();
        return service;
    }

    /// <summary>Converting at the trial end starts the paid period at the end.</summary>
    [Fact]
    public void BeginTrialConversion_AtTheEnd_StartsThePeriodAtTheTrialEnd()
    {
        var service = ActiveTrial();
        var end = service.TrialEndsAt!.Value;

        service.BeginTrialConversion(end.AddHours(2));

        service.TrialStatus.Should().Be(TrialStatus.AwaitingPayment);
        service.NextRenewalAt.Should().Be(end);
        service.TrialEndsAt.Should().Be(end);
        service.TrialPaymentDueAt.Should().Be(end.AddDays(3));
        service.DomainEvents.OfType<TrialConvertedEvent>().Should().ContainSingle();
    }

    /// <summary>Converting early starts the paid period now.</summary>
    [Fact]
    public void BeginTrialConversion_Early_StartsThePeriodNow()
    {
        var service = ActiveTrial();
        var now = Now.AddDays(5);

        service.BeginTrialConversion(now);

        service.TrialEndsAt.Should().Be(now);
        service.NextRenewalAt.Should().Be(now);
        service.TrialPaymentDueAt.Should().Be(now.AddDays(3));
    }

    /// <summary>A conversion that runs long after the end is due the grace days from now.</summary>
    [Fact]
    public void BeginTrialConversion_RunLate_DueIsNowPlusGrace()
    {
        var service = ActiveTrial();
        var now = service.TrialEndsAt!.Value.AddDays(10);

        service.BeginTrialConversion(now);

        service.TrialPaymentDueAt.Should().Be(now.AddDays(3));
    }

    /// <summary>A trial whose service is not Active cannot be converted.</summary>
    [Fact]
    public void BeginTrialConversion_OfAPendingTrial_ThrowsServiceNotActive()
    {
        var service = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        service.StartTrial(Rules, Now);

        var act = () => service.BeginTrialConversion(Now);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(ServicesErrors.ServiceNotActive);
    }

    /// <summary>An ordinary service is not a trial.</summary>
    [Fact]
    public void BeginTrialConversion_OfAnOrdinaryService_ThrowsServiceNotInTrial()
    {
        var service = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        service.ActivateDigital(Now);

        var act = () => service.BeginTrialConversion(Now);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(ServicesErrors.ServiceNotInTrial);
    }

    /// <summary>Only an awaiting-payment trial completes.</summary>
    [Fact]
    public void CompleteTrialConversion_OnlyMovesAwaitingPayment()
    {
        var ordinary = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        var active = ActiveTrial();
        var awaiting = ActiveTrial();
        awaiting.BeginTrialConversion(Now.AddDays(1));

        ordinary.CompleteTrialConversion().Should().BeFalse();
        active.CompleteTrialConversion().Should().BeFalse();
        active.TrialStatus.Should().Be(TrialStatus.Active);
        awaiting.CompleteTrialConversion().Should().BeTrue();
        awaiting.TrialStatus.Should().Be(TrialStatus.ConvertedToPaid);
        awaiting.CompleteTrialConversion().Should().BeFalse();
    }

    /// <summary>Terminating a running trial ends it and raises the event.</summary>
    [Fact]
    public void Terminate_OfARunningTrial_SetsTerminatedAndRaisesTrialEnded()
    {
        var active = ActiveTrial();
        var awaiting = ActiveTrial();
        awaiting.BeginTrialConversion(Now.AddDays(1));
        var pending = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        pending.StartTrial(Rules, Now);

        foreach (var service in new[] { active, awaiting, pending })
        {
            service.ClearDomainEvents();
            service.Terminate(Now.AddDays(2));

            service.TrialStatus.Should().Be(TrialStatus.Terminated);
            service.DomainEvents.OfType<TrialEndedEvent>().Should().ContainSingle();
        }
    }

    /// <summary>A converted trial is an ordinary service when terminated.</summary>
    [Fact]
    public void Terminate_OfAConvertedTrial_KeepsConvertedToPaid()
    {
        var service = ActiveTrial();
        service.BeginTrialConversion(Now.AddDays(1));
        service.CompleteTrialConversion();
        service.ClearDomainEvents();

        service.Terminate(Now.AddDays(40));

        service.TrialStatus.Should().Be(TrialStatus.ConvertedToPaid);
        service.DomainEvents.OfType<TrialEndedEvent>().Should().BeEmpty();
    }

    /// <summary>The reminder day is the smallest one not below the days left.</summary>
    [Fact]
    public void DueTrialReminder_PicksTheSmallestDayNotBelowTheDaysLeft()
    {
        var service = ActiveTrial();
        var end = service.TrialEndsAt!.Value;

        service.DueTrialReminder(end.AddDays(-3)).Should().Be(3);
        service.DueTrialReminder(end.AddDays(-2)).Should().Be(3);
        service.DueTrialReminder(end.AddDays(-1)).Should().Be(1);
        service.DueTrialReminder(end.AddDays(-4)).Should().BeNull();
        service.DueTrialReminder(end).Should().BeNull();
        service.DueTrialReminder(end.AddDays(1)).Should().BeNull();

        var awaiting = ActiveTrial();
        awaiting.BeginTrialConversion(end.AddDays(-2));
        awaiting.DueTrialReminder(end.AddDays(-2)).Should().BeNull();
    }

    /// <summary>A trial whose service was never set up (still Pending) gets no reminder before it is Active.</summary>
    [Fact]
    public void DueTrialReminder_OfAPendingTrial_IsNull()
    {
        var service = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        service.StartTrial(Rules, Now);
        var end = service.TrialEndsAt!.Value;

        service.DueTrialReminder(end.AddDays(-3)).Should().BeNull();
        service.DueTrialReminder(end.AddDays(-1)).Should().BeNull();
    }

    /// <summary>Terminating an ordinary service raises no trial event and leaves the trial status empty.</summary>
    [Fact]
    public void Terminate_OfAnOrdinaryService_RaisesNoTrialEnded()
    {
        var service = ClientService.Create(1, 2, BillingCycle.Monthly, Now);
        service.ActivateDigital(Now);
        service.ClearDomainEvents();

        service.Terminate(Now.AddDays(2));

        service.TrialStatus.Should().BeNull();
        service.DomainEvents.OfType<TrialEndedEvent>().Should().BeEmpty();
    }

    /// <summary>
    /// A trial terminated before its end reports the termination as its end date; one terminated after its end
    /// reports the end.
    /// </summary>
    [Fact]
    public void Terminate_OfARunningTrial_ReportsTheEarlierOfTheEndAndNow()
    {
        var early = ActiveTrial();
        var late = ActiveTrial();
        var end = late.TrialEndsAt!.Value;

        early.Terminate(Now.AddDays(2));
        late.Terminate(end.AddDays(5));

        early.DomainEvents.OfType<TrialEndedEvent>().Should().ContainSingle().Which.TrialEndsAt.Should().Be(Now.AddDays(2));
        late.DomainEvents.OfType<TrialEndedEvent>().Should().ContainSingle().Which.TrialEndsAt.Should().Be(end);
    }

    /// <summary>
    /// While a trial is running (Active or awaiting payment) its next renewal date cannot be moved; the other billing
    /// terms still change when the date is given unchanged.
    /// </summary>
    [Fact]
    public void ChangeBillingTerms_OfARunningTrial_RefusesANewDueDateOnly()
    {
        var active = ActiveTrial();
        var awaiting = ActiveTrial();
        awaiting.BeginTrialConversion(Now.AddDays(1));

        foreach (var service in new[] { active, awaiting })
        {
            var renewal = service.NextRenewalAt;
            var moved = () => service.ChangeBillingTerms(Terms(renewal!.Value.AddDays(7), 20m));

            moved.Should().Throw<DomainRuleException>().Which.Code.Should().Be(ServicesErrors.ServiceTrialDueDateFixed);
            service.NextRenewalAt.Should().Be(renewal);
            service.RecurringAmount.Should().NotBe(20m);

            service.ChangeBillingTerms(Terms(renewal, 20m));
            service.RecurringAmount.Should().Be(20m);
            service.NextRenewalAt.Should().Be(renewal);
        }
    }

    /// <summary>A trial converted to a paid service is billed like any other: its next renewal date can be moved.</summary>
    [Fact]
    public void ChangeBillingTerms_OfAConvertedTrial_MovesTheDueDate()
    {
        var service = ActiveTrial();
        service.BeginTrialConversion(Now.AddDays(1));
        service.CompleteTrialConversion();
        var moved = Now.AddDays(60);

        service.ChangeBillingTerms(Terms(moved, 20m));

        service.NextRenewalAt.Should().Be(moved);
    }

    /// <summary>Builds monthly billing terms with the given next renewal date and recurring amount.</summary>
    /// <param name="nextRenewalAt">The next renewal date.</param>
    /// <param name="recurring">The recurring amount.</param>
    /// <returns>The terms.</returns>
    private static ServiceBillingTerms Terms(DateTimeOffset? nextRenewalAt, decimal recurring) =>
        new(BillingCycle.Monthly, 1, 0m, recurring, nextRenewalAt, null, null, null);

    /// <summary>A recorded reminder raises the event with the trial end.</summary>
    [Fact]
    public void TrialReminderCreate_RaisesTrialEndingSoon()
    {
        var service = ActiveTrial();

        var reminder = TrialReminder.Create(service, 3, Now.AddDays(11));

        reminder.DaysBefore.Should().Be(3);
        reminder.SentAt.Should().Be(Now.AddDays(11));
        reminder.DomainEvents.OfType<TrialEndingSoonEvent>().Should().ContainSingle()
            .Which.TrialEndsAt.Should().Be(service.TrialEndsAt);
    }
}
