using Innovayse.Hostpanel.Application.Billing.Currencies.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings.Enums;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IExchangeRateSource"/> that quotes what a test told it to and records what it was
/// asked for. A fake port standing in for the upstream rate service, which a test must not call.
/// </summary>
/// <param name="quotes">The rates to answer with, keyed by currency code.</param>
/// <param name="failureCode">When set, every call fails with this code instead of quoting.</param>
public sealed class FakeExchangeRateSource(IReadOnlyDictionary<string, decimal> quotes, string? failureCode = null)
    : IExchangeRateSource
{
    /// <summary>The codes the last call asked to be quoted.</summary>
    public IReadOnlyList<string> RequestedCodes { get; private set; } = [];

    /// <summary>The bank the last call was told to ask.</summary>
    public ExchangeRateProvider? RequestedProvider { get; private set; }

    /// <summary>The base currency the last call asked the rates against.</summary>
    public string? RequestedBase { get; private set; }

    /// <inheritdoc />
    public Task<Result<ExchangeRateQuotes>> QuoteAsync(ExchangeRateLookup lookup, CancellationToken ct)
    {
        RequestedProvider = lookup.Provider;
        RequestedBase = lookup.BaseCode;
        RequestedCodes = [.. lookup.Codes];

        if (failureCode is not null)
        {
            return Task.FromResult(Result<ExchangeRateQuotes>.FailureCode(failureCode));
        }

        IReadOnlyDictionary<string, decimal> answer = RequestedCodes
            .Where(quotes.ContainsKey)
            .ToDictionary(code => code, code => quotes[code]);

        return Task.FromResult(Result<ExchangeRateQuotes>.Success(new ExchangeRateQuotes(answer)));
    }
}
