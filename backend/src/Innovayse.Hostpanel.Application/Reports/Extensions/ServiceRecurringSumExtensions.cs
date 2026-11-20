using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Application.Reports.Extensions;

/// <summary>Brings recurring service charges to one monthly figure in the base currency, for the revenue reports.</summary>
public static class ServiceRecurringSumExtensions
{
    /// <summary>Months in a year, the factor of an annual figure.</summary>
    private const int MonthsInYear = 12;

    /// <summary>Converts the sums to the base currency and spreads each over the months of its cycle.</summary>
    /// <param name="sums">The recurring sums of one group of services.</param>
    /// <param name="rates">The base currency and the rates to it.</param>
    /// <returns>The monthly amount in the base currency. Each sum is converted and rounded to the base currency's minor unit first (<see cref="BaseCurrencyRates.ToBase"/>), then spread over its cycle without further rounding, so a caller rounds once after any further arithmetic.</returns>
    public static decimal MonthlyInBase(this IEnumerable<ServiceRecurringSum> sums, BaseCurrencyRates rates)
    {
        ArgumentNullException.ThrowIfNull(sums);
        ArgumentNullException.ThrowIfNull(rates);

        return sums.Sum(s => rates.ToBase(s.Amount, s.Currency ?? rates.Currency) / RenewalPeriod.MonthsIn(s.BillingCycle));
    }

    /// <summary>Rounds a monthly amount to the base currency's minor unit.</summary>
    /// <param name="monthly">The unrounded monthly amount from <see cref="MonthlyInBase"/>.</param>
    /// <param name="rates">The base currency and the rates to it.</param>
    /// <returns>The monthly amount rounded to the minor unit.</returns>
    public static decimal RoundedMonthly(this decimal monthly, BaseCurrencyRates rates)
    {
        ArgumentNullException.ThrowIfNull(rates);

        return CurrencyCode.RoundToMinorUnits(monthly, rates.Currency);
    }

    /// <summary>Projects a monthly amount over a year and rounds it to the base currency's minor unit.</summary>
    /// <param name="monthly">The unrounded monthly amount from <see cref="MonthlyInBase"/>.</param>
    /// <param name="rates">The base currency and the rates to it.</param>
    /// <returns>Twelve months of the amount, rounded to the minor unit.</returns>
    public static decimal RoundedAnnual(this decimal monthly, BaseCurrencyRates rates)
    {
        ArgumentNullException.ThrowIfNull(rates);

        return CurrencyCode.RoundToMinorUnits(monthly * MonthsInYear, rates.Currency);
    }
}
