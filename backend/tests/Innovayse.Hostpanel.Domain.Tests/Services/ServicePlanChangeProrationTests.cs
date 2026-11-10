using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Services;

/// <summary>Unit tests for <see cref="ServicePlanChangeProration"/>: the one rule for the cost of a mid-cycle plan change.</summary>
public sealed class ServicePlanChangeProrationTests
{
    /// <summary>The end of the monthly cycle the tests change plan in; the cycle started on 1 November and has 30 days.</summary>
    private static readonly DateTimeOffset Renewal = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>An upgrade charges the price difference for the share of the cycle that is left.</summary>
    [Fact]
    public void Calculate_Upgrade_ChargesTheDifferenceForTheRemainingShare()
    {
        var change = ServicePlanChangeProration.Calculate(
            10m, 20m, BillingCycle.Monthly, Renewal, new DateTimeOffset(2026, 11, 16, 0, 0, 0, TimeSpan.Zero), "USD");

        change.Should().Be(new ProratedPlanChange(ServiceChangeType.Upgrade, 5m));
    }

    /// <summary>A downgrade credits the price difference for the share of the cycle that is left.</summary>
    [Fact]
    public void Calculate_Downgrade_CreditsTheDifferenceForTheRemainingShare()
    {
        var change = ServicePlanChangeProration.Calculate(
            20m, 10m, BillingCycle.Monthly, Renewal, new DateTimeOffset(2026, 11, 16, 0, 0, 0, TimeSpan.Zero), "USD");

        change.Should().Be(new ProratedPlanChange(ServiceChangeType.Downgrade, 5m));
    }

    /// <summary>Two plans at the same price have nothing to prorate and are refused with no result.</summary>
    [Fact]
    public void Calculate_EqualPrices_ReturnsNull()
    {
        var change = ServicePlanChangeProration.Calculate(
            10m, 10m, BillingCycle.Monthly, Renewal, new DateTimeOffset(2026, 11, 16, 0, 0, 0, TimeSpan.Zero), "USD");

        change.Should().BeNull();
    }

    /// <summary>On the renewal moment itself no time is left, so the change costs nothing.</summary>
    [Fact]
    public void Calculate_AtTheRenewalMoment_IsZero()
    {
        var change = ServicePlanChangeProration.Calculate(10m, 20m, BillingCycle.Monthly, Renewal, Renewal, "USD");

        change.Should().Be(new ProratedPlanChange(ServiceChangeType.Upgrade, 0m));
    }

    /// <summary>A renewal date already in the past (an overdue service) is zero days, never a negative amount.</summary>
    [Fact]
    public void Calculate_AfterTheRenewalDate_IsZeroAndNeverNegative()
    {
        var change = ServicePlanChangeProration.Calculate(
            20m, 10m, BillingCycle.Monthly, Renewal, Renewal.AddDays(5), "USD");

        change.Should().Be(new ProratedPlanChange(ServiceChangeType.Downgrade, 0m));
    }

    /// <summary>The last day of the cycle is one day of thirty, and a started day counts as a whole one.</summary>
    /// <param name="hour">The hour of 30 November the change is made at.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    public void Calculate_OnTheLastDay_IsOneDayOfTheCycle(int hour)
    {
        var change = ServicePlanChangeProration.Calculate(
            0m, 30m, BillingCycle.Monthly, Renewal, new DateTimeOffset(2026, 11, 30, hour, 0, 0, TimeSpan.Zero), "USD");

        change.Should().Be(new ProratedPlanChange(ServiceChangeType.Upgrade, 1m));
    }

    /// <summary>A renewal date pushed further out than one cycle never charges more than the whole cycle's difference.</summary>
    [Fact]
    public void Calculate_RenewalMoreThanACycleAway_IsCappedAtTheWholeDifference()
    {
        var change = ServicePlanChangeProration.Calculate(
            10m, 20m, BillingCycle.Monthly, Renewal, new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), "USD");

        change.Should().Be(new ProratedPlanChange(ServiceChangeType.Upgrade, 10m));
    }

    /// <summary>The amount is rounded to the cents of a two-decimal currency, half away from zero.</summary>
    [Fact]
    public void Calculate_RoundsToTheMinorUnitsOfTheCurrency()
    {
        // 10 of 30 days remain: a difference of 10 is 3.3333.
        var change = ServicePlanChangeProration.Calculate(
            0m, 10m, BillingCycle.Monthly, Renewal, new DateTimeOffset(2026, 11, 21, 0, 0, 0, TimeSpan.Zero), "USD");

        change.Should().Be(new ProratedPlanChange(ServiceChangeType.Upgrade, 3.33m));
    }

    /// <summary>A zero-decimal currency is rounded to whole units, the three-decimal one to thousandths.</summary>
    /// <param name="currency">The ISO 4217 code of the client's billing currency.</param>
    /// <param name="expected">The amount for a difference of 1000 with 10 of 30 days left.</param>
    [Theory]
    [InlineData("JPY", 333)]
    [InlineData("KWD", 333.333)]
    public void Calculate_UsesTheMinorUnitsOfTheBillingCurrency(string currency, double expected)
    {
        var change = ServicePlanChangeProration.Calculate(
            0m, 1000m, BillingCycle.Monthly, Renewal, new DateTimeOffset(2026, 11, 21, 0, 0, 0, TimeSpan.Zero), currency);

        change!.Amount.Should().Be((decimal)expected);
    }

    /// <summary>The cycle length follows the service's billing cycle: a quarter counted back from the renewal is 91 days in this case.</summary>
    [Fact]
    public void Calculate_QuarterlyCycle_UsesTheLengthOfThatCycle()
    {
        var renewal = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

        // The quarter ended 1 December began 1 September: 91 days. 91 days left is the whole difference.
        var whole = ServicePlanChangeProration.Calculate(
            0m, 91m, BillingCycle.Quarterly, renewal, new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), "USD");
        var half = ServicePlanChangeProration.Calculate(
            0m, 91m, BillingCycle.Quarterly, renewal, renewal.AddDays(-13), "USD");

        whole!.Amount.Should().Be(91m);
        half!.Amount.Should().Be(13m);
    }

    /// <summary>A negative price is a bug in the caller and is refused.</summary>
    [Fact]
    public void Calculate_NegativePrice_Throws()
    {
        var act = () => ServicePlanChangeProration.Calculate(-1m, 10m, BillingCycle.Monthly, Renewal, Renewal.AddDays(-5), "USD");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
