using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Domain.Services;

/// <summary>
/// The one place the cost of changing a service's plan in the middle of a billing cycle is worked out.
/// </summary>
/// <remarks>
/// The difference between the two recurring prices is charged (upgrade) or credited (downgrade) for the share of the
/// current cycle that is left: <c>|new - old| x DaysRemaining / CycleLength</c>. The cycle ends at the service's next
/// renewal date and starts one billing cycle (<see cref="RenewalPeriod.MonthsIn"/>) before it, so the length is counted
/// in real days of that cycle. Pure: the moment is passed in, nothing is read or written.
/// </remarks>
public static class ServicePlanChangeProration
{
    /// <summary>Works out the type and the prorated amount of moving a service to another recurring price.</summary>
    /// <param name="currentPrice">The service's current recurring price per cycle, in the client's billing currency.</param>
    /// <param name="newPrice">The new plan's price for the same cycle, in the same currency.</param>
    /// <param name="cycle">The service's billing cycle.</param>
    /// <param name="nextRenewalAt">When the current cycle ends and the next one is billed.</param>
    /// <param name="now">The moment of the change (UTC).</param>
    /// <param name="currencyCode">ISO 4217 alpha code the amount is rounded to the minor units of.</param>
    /// <returns>The change, or <see langword="null"/> when the two prices are equal and there is nothing to prorate.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A price is negative.</exception>
    public static ProratedPlanChange? Calculate(
        decimal currentPrice, decimal newPrice, BillingCycle cycle, DateTimeOffset nextRenewalAt, DateTimeOffset now, string currencyCode)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentPrice);
        ArgumentOutOfRangeException.ThrowIfNegative(newPrice);

        if (newPrice == currentPrice)
        {
            return null;
        }

        var type = newPrice > currentPrice ? ServiceChangeType.Upgrade : ServiceChangeType.Downgrade;
        var prorated = Math.Abs(newPrice - currentPrice) * RemainingShare(cycle, nextRenewalAt, now);
        return new ProratedPlanChange(type, CurrencyCode.RoundToMinorUnits(prorated, currencyCode));
    }

    /// <summary>The share of the current billing cycle that is left at <paramref name="now"/>.</summary>
    /// <param name="cycle">The service's billing cycle.</param>
    /// <param name="nextRenewalAt">When the current cycle ends.</param>
    /// <param name="now">The moment of the change (UTC).</param>
    /// <returns>
    /// A share from 0 to 1: a started day counts as a whole day, a cycle that has ended counts as 0, and a renewal date
    /// pushed further out than one cycle counts as the whole cycle.
    /// </returns>
    private static decimal RemainingShare(BillingCycle cycle, DateTimeOffset nextRenewalAt, DateTimeOffset now)
    {
        var cycleDays = (int)(nextRenewalAt - nextRenewalAt.AddMonths(-RenewalPeriod.MonthsIn(cycle))).TotalDays;
        var daysRemaining = (int)Math.Ceiling((nextRenewalAt - now).TotalDays);
        return (decimal)Math.Clamp(daysRemaining, 0, cycleDays) / cycleDays;
    }
}
