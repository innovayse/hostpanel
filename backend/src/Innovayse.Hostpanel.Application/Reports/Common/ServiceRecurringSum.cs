using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Application.Reports.Common;

/// <summary>
/// What the active services of one group charge on each renewal, split by the currency they are billed in and the
/// cycle they renew on, so a report can bring them to one monthly figure in the base currency.
/// </summary>
/// <param name="Currency">ISO 4217 code of the owning client's billing currency, or <see langword="null"/> when the client bills in the base currency.</param>
/// <param name="BillingCycle">The cycle the services renew on.</param>
/// <param name="Amount">Sum of the services' recurring amounts, in <paramref name="Currency"/>.</param>
/// <param name="Services">How many services the sum covers.</param>
public sealed record ServiceRecurringSum(string? Currency, BillingCycle BillingCycle, decimal Amount, int Services);
