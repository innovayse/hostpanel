using Innovayse.Hostpanel.Application.Fraud.Common;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Fraud.Queries.GetFraudSettings;

/// <summary>Answers with the fraud detection settings, the defaults for whatever the operator never saved.</summary>
public sealed class GetFraudSettingsHandler
{
    /// <summary>Reads the stored settings and falls back to the defaults.</summary>
    private readonly FraudSettingsReader _settings;

    /// <summary>Initializes the handler.</summary>
    /// <param name="settings">Reads the stored settings and falls back to the defaults.</param>
    public GetFraudSettingsHandler(FraudSettingsReader settings)
    {
        _settings = settings;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the settings. Never refuses.</returns>
    public async Task<Result<FraudSettingsDto>> HandleAsync(GetFraudSettingsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        return Result.Success(await _settings.ReadAsync(ct));
    }
}
