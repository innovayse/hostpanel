using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.Billing.Currencies.Commands.UpdateExchangeRatesCron;

/// <summary>
/// Scheduled command that refreshes the exchange rates.
/// Dispatched daily by <c>BillingScheduledJobsStartup</c> at the hour set in <c>ScheduledJobsOptions</c>.
/// </summary>
/// <remarks>
/// Rates alone, like the admin panel's update button: the price list is not recomputed here, that stays the
/// operator's decision (<c>UpdateProductPricesCommand</c>).
/// </remarks>
public sealed record UpdateExchangeRatesCronCommand : IRecurringJob;
