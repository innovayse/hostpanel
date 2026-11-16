using Innovayse.Hostpanel.Domain.Settings.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Currencies.Common;

/// <summary>What <c>IExchangeRateSource</c> is asked to quote.</summary>
/// <param name="Provider">The bank the operator chose to ask; the source answers from it and from no other.</param>
/// <param name="BaseCode">ISO 4217 alpha code of the base currency, any case.</param>
/// <param name="Codes">ISO 4217 alpha codes to quote against the base, any case.</param>
public sealed record ExchangeRateLookup(ExchangeRateProvider Provider, string BaseCode, IReadOnlyList<string> Codes);
