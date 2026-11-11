using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Domain.Settings;

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>
/// Reads the plan change switch of <see cref="BillingSettingKeys"/>. A missing or unparsable row falls back to the
/// seeded default with a warning: a typo in System Settings must not approve downgrades nobody meant to approve.
/// The row is read through <see cref="SettingSource"/>.
/// </summary>
public sealed class ServicePlanChangeSettingsReader
{
    /// <summary>Reads the row and falls back to the default.</summary>
    private readonly SettingSource _source;

    /// <summary>Initializes the reader.</summary>
    /// <param name="source">Reads the row and falls back to the default.</param>
    public ServicePlanChangeSettingsReader(SettingSource source)
    {
        _source = source;
    }

    /// <summary>Reads whether a client's downgrade is approved at once.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when downgrades are approved without staff; the default is <see langword="false"/>.</returns>
    public async Task<bool> AutoApproveDowngradesAsync(CancellationToken ct)
    {
        var rows = await _source.ReadRowsAsync([BillingSettingKeys.AutoApproveDowngrades], ct);
        return bool.Parse(_source.Value(rows, BillingSettingKeys.AutoApproveDowngrades));
    }
}
