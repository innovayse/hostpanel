using System.Globalization;
using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Queries.GetSettingValues;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// Reads the two top-up limits of <see cref="BillingSettingKeys"/> into <see cref="CreditTopUpSettings"/>. A missing,
/// unparsable or contradictory pair (a minimum above the maximum) falls back to the seeded defaults with a warning: a typo
/// in System Settings must not let a client add funds without any limit. The settings belong to the Admin module, so the rows
/// are read through its <see cref="GetSettingValuesQuery"/>.
/// </summary>
public sealed class CreditTopUpSettingsReader
{
    /// <summary>Dispatches the Admin settings query.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Warns about a fallback.</summary>
    private readonly ILogger<CreditTopUpSettingsReader> _logger;

    /// <summary>Initializes the reader.</summary>
    /// <param name="bus">Dispatches the Admin settings query.</param>
    /// <param name="logger">Warns about a fallback.</param>
    public CreditTopUpSettingsReader(IMessageBus bus, ILogger<CreditTopUpSettingsReader> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    /// <summary>Reads the top-up limits.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The parsed limits, the defaults when the stored pair is unusable.</returns>
    public async Task<CreditTopUpSettings> ReadAsync(CancellationToken ct)
    {
        var keys = new List<string> { BillingSettingKeys.CreditTopUpMinimum, BillingSettingKeys.CreditTopUpMaximum };
        var result = await _bus.InvokeAsync<Result<IReadOnlyList<SettingDto>>>(new GetSettingValuesQuery(keys), ct);
        IReadOnlyDictionary<string, string> rows = (result.Value ?? [])
            .Where(s => s.Value is not null)
            .ToDictionary(s => s.Key, s => s.Value!, StringComparer.Ordinal);

        var minimum = Parse(rows, BillingSettingKeys.CreditTopUpMinimum);
        var maximum = Parse(rows, BillingSettingKeys.CreditTopUpMaximum);
        if (minimum <= maximum)
        {
            return new CreditTopUpSettings(minimum, maximum);
        }

        _logger.LogWarning("The top-up minimum {Minimum} is above the maximum {Maximum}; using the defaults.", minimum, maximum);
        return new CreditTopUpSettings(Default(BillingSettingKeys.CreditTopUpMinimum), Default(BillingSettingKeys.CreditTopUpMaximum));
    }

    /// <summary>The seeded default of <paramref name="key"/>.</summary>
    /// <param name="key">A top-up limit key.</param>
    /// <returns>Its default amount.</returns>
    private static decimal Default(string key) =>
        decimal.Parse(BillingSettingKeys.Defaults.First(d => d.Key == key).Value, CultureInfo.InvariantCulture);

    /// <summary>Reads a stored amount, or the default with a warning when it is missing or does not match its pattern.</summary>
    /// <param name="rows">The stored rows.</param>
    /// <param name="key">A top-up limit key.</param>
    /// <returns>An amount above zero.</returns>
    private decimal Parse(IReadOnlyDictionary<string, string> rows, string key)
    {
        if (rows.TryGetValue(key, out var value) && BillingSettingKeys.Patterns[key].Regex.IsMatch(value))
        {
            return decimal.Parse(value, CultureInfo.InvariantCulture);
        }

        _logger.LogWarning("Billing setting {Key} is missing or invalid; using its default.", key);
        return Default(key);
    }
}
