using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;

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
}
