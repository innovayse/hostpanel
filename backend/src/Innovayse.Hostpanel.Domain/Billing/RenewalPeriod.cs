using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Domain.Billing;

/// <summary>
/// The one place renewal periods are computed. A billing cycle is turned into months here and
/// nowhere else, so adding a cycle changes this file only.
/// </summary>
public static class RenewalPeriod
{
    /// <summary>
    /// The end of the period that starts at <paramref name="start"/> for a service on
    /// <paramref name="cycle"/>.
    /// </summary>
    /// <remarks>
    /// The end is <paramref name="start"/> plus one cycle. The only correction is for a start that
    /// was clamped to a short month's last day: when <paramref name="start"/> is the last day of its
    /// month and earlier than <paramref name="anchorDay"/>, the end is moved towards the anchor, so a
    /// service bought on the 31st renews on Feb 28 and then on Mar 31 instead of creeping to the 28th
    /// forever. Any other start, including a date an admin edited off the anchor, gets exactly
    /// one cycle, never extra days.
    /// </remarks>
    /// <param name="start">The period start (the service's current <c>NextRenewalAt</c>).</param>
    /// <param name="cycle">The service's billing cycle.</param>
    /// <param name="anchorDay">The day of month the service was bought on (1-31).</param>
    /// <returns>The period end.</returns>
    public static DateTimeOffset EndOf(DateTimeOffset start, BillingCycle cycle, int anchorDay)
    {
        var end = start.AddMonths(MonthsIn(cycle));
        var clampedToMonthEnd = start.Day < anchorDay && start.Day == DateTime.DaysInMonth(start.Year, start.Month);
        if (!clampedToMonthEnd)
        {
            return end;
        }

        var wantedDay = Math.Min(anchorDay, DateTime.DaysInMonth(end.Year, end.Month));
        return end.Day < wantedDay ? end.AddDays(wantedDay - end.Day) : end;
    }

    /// <summary>The end of a domain renewal period of <paramref name="years"/> years.</summary>
    /// <param name="start">The period start (the domain's current expiry).</param>
    /// <param name="years">The registration period; anything below one counts as one year.</param>
    /// <returns>The period end.</returns>
    public static DateTimeOffset EndOfYears(DateTimeOffset start, int years) => start.AddYears(Math.Max(1, years));

    /// <summary>The length of a cycle in months.</summary>
    /// <param name="cycle">A parsed cycle.</param>
    /// <returns>The number of months the cycle covers.</returns>
    private static int MonthsIn(BillingCycle cycle) => cycle switch
    {
        BillingCycle.Monthly => 1,
        BillingCycle.Annual => 12,
        BillingCycle.Quarterly => 3,
        BillingCycle.SemiAnnual => 6,
        BillingCycle.Biennial => 24,
        _ => throw new ArgumentOutOfRangeException(nameof(cycle), cycle, "No month count for this cycle."),
    };
}
