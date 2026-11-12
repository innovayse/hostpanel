using System.Globalization;
using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Fraud.Common;

/// <summary>
/// Reads <see cref="FraudSettingKeys"/> into <see cref="FraudSettingsDto"/> through <see cref="SettingSource"/>, so a row that was
/// never saved reads as its default. A pair of thresholds that contradicts itself (the review score not below the reject score,
/// which only an edit of a single row in System Settings can produce) falls back to the default pair with a warning: a typo
/// must not turn every order into a refusal or switch the review off.
/// </summary>
public sealed class FraudSettingsReader
{
    /// <summary>Reads the rows and falls back to the defaults.</summary>
    private readonly SettingSource _source;

    /// <summary>Warns about a contradictory pair.</summary>
    private readonly ILogger<FraudSettingsReader> _logger;

    /// <summary>Initializes the reader.</summary>
    /// <param name="source">Reads the rows and falls back to the defaults.</param>
    /// <param name="logger">Warns about a contradictory pair.</param>
    public FraudSettingsReader(SettingSource source, ILogger<FraudSettingsReader> logger)
    {
        _source = source;
        _logger = logger;
    }

    /// <summary>Reads the settings.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The settings; the defaults for whatever is missing or unusable.</returns>
    public async Task<FraudSettingsDto> ReadAsync(CancellationToken ct)
    {
        var rows = await _source.ReadRowsAsync(FraudSettingKeys.All, ct);

        var review = ParseInt(rows, FraudSettingKeys.ManualReviewRiskScore);
        var reject = ParseInt(rows, FraudSettingKeys.AutoRejectRiskScore);
        if (review >= reject)
        {
            _logger.LogWarning(
                "The fraud review score {Review} is not below the auto-reject score {Reject}; using the defaults.", review, reject);
            review = int.Parse(SettingSource.Default(FraudSettingKeys.ManualReviewRiskScore), CultureInfo.InvariantCulture);
            reject = int.Parse(SettingSource.Default(FraudSettingKeys.AutoRejectRiskScore), CultureInfo.InvariantCulture);
        }

        return new FraudSettingsDto(
            bool.Parse(_source.Value(rows, FraudSettingKeys.Enabled)),
            review,
            reject,
            ParseInt(rows, FraudSettingKeys.VelocityWindowMinutes),
            ParseInt(rows, FraudSettingKeys.VelocityMaxOrders),
            ParseInt(rows, FraudSettingKeys.MaxAccountsPerIp));
    }

    /// <summary>Reads a stored number, or the default when it is missing or does not match its pattern.</summary>
    /// <param name="rows">The stored rows.</param>
    /// <param name="key">A numeric fraud setting key.</param>
    /// <returns>The number.</returns>
    private int ParseInt(IReadOnlyDictionary<string, string> rows, string key)
    {
        return int.Parse(_source.Value(rows, key), CultureInfo.InvariantCulture);
    }
}
