using Innovayse.Hostpanel.Application.Admin.Queries.GetSettingValues;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Admin.Common;

/// <summary>
/// The one place a row of the general settings catalogue (<see cref="GeneralSettingKeys"/>) is read and checked by another
/// module: it fetches the stored rows through the Admin module's <see cref="GetSettingValuesQuery"/> and answers each key with
/// the stored value, or its seeded default with a warning when the row is missing or does not match the key's pattern. The
/// billing and campaign readers build their typed settings on it, so a typo in System Settings falls back the same way
/// everywhere and no handler parses a setting string itself.
/// </summary>
public sealed class SettingSource
{
    /// <summary>Dispatches the Admin settings query.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Warns about a fallback.</summary>
    private readonly ILogger<SettingSource> _logger;

    /// <summary>Initializes the source.</summary>
    /// <param name="bus">Dispatches the Admin settings query.</param>
    /// <param name="logger">Warns about a fallback.</param>
    public SettingSource(IMessageBus bus, ILogger<SettingSource> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    /// <summary>The seeded default of <paramref name="key"/>.</summary>
    /// <param name="key">A catalogue key.</param>
    /// <returns>Its default value.</returns>
    public static string Default(string key) => GeneralSettingKeys.Defaults.First(d => d.Key == key).Value;

    /// <summary>Reads the stored rows of the keys.</summary>
    /// <param name="keys">The catalogue keys to read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stored values by key; a key without a row, or a secret row that carries no value, is absent.</returns>
    public async Task<IReadOnlyDictionary<string, string>> ReadRowsAsync(IReadOnlyList<string> keys, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<Result<IReadOnlyList<SettingDto>>>(new GetSettingValuesQuery(keys), ct);

        // A secret setting carries no value; no catalogue key is secret, so such a row reads as missing.
        return (result.Value ?? [])
            .Where(s => s.Value is not null)
            .ToDictionary(s => s.Key, s => s.Value!, StringComparer.Ordinal);
    }

    /// <summary>Reads a stored value, or the default with a warning when it is missing or does not match its pattern.</summary>
    /// <param name="rows">The stored rows, from <see cref="ReadRowsAsync"/>.</param>
    /// <param name="key">The setting key, one with a pattern.</param>
    /// <returns>A value that matches the key's pattern (or is an allowed empty).</returns>
    public string Value(IReadOnlyDictionary<string, string> rows, string key)
    {
        if (rows.TryGetValue(key, out var value)
            && ((value.Length == 0 && GeneralSettingKeys.AllowsEmpty(key))
                || (GeneralSettingKeys.TryGetPattern(key, out var pattern) && pattern.Regex.IsMatch(value))))
        {
            return value;
        }

        _logger.LogWarning("Setting {Key} is missing or invalid; using its default.", key);
        return Default(key);
    }
}
