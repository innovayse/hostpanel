using System.Globalization;
using Innovayse.Hostpanel.Application.Admin.Commands.UpdateSetting;
using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Queries.GetSettingValues;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Fraud.Commands.UpdateFraudSettings;

/// <summary>
/// Saves the fraud settings through the Admin module's own settings use cases, so the stored rows are checked and written exactly
/// as a save from System Settings is. The cross-field rule (review below reject) is the validator's; this handler only writes.
/// </summary>
public sealed class UpdateFraudSettingsHandler
{
    /// <summary>The message bus, for the Admin module's settings query and command; Fraud never touches the settings table.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">The message bus, for the Admin module's settings query and command.</param>
    public UpdateFraudSettingsHandler(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The settings to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>SETTING_NOT_FOUND</c> when a fraud setting row was never seeded, or whatever the Admin module refuses a value with.</returns>
    public async Task<Result> HandleAsync(UpdateFraudSettingsCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var stored = await _bus.InvokeAsync<Result<IReadOnlyList<SettingDto>>>(new GetSettingValuesQuery(FraudSettingKeys.All), ct);
        var rows = (stored.Value ?? []).ToDictionary(s => s.Key, StringComparer.Ordinal);

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [FraudSettingKeys.Enabled] = cmd.Enabled ? IntegrationSettingKeys.EnabledValue : IntegrationSettingKeys.DisabledValue,
            [FraudSettingKeys.ManualReviewRiskScore] = Text(cmd.ManualReviewRiskScore),
            [FraudSettingKeys.AutoRejectRiskScore] = Text(cmd.AutoRejectRiskScore),
            [FraudSettingKeys.VelocityWindowMinutes] = Text(cmd.VelocityWindowMinutes),
            [FraudSettingKeys.VelocityMaxOrders] = Text(cmd.VelocityMaxOrders),
            [FraudSettingKeys.MaxAccountsPerIp] = Text(cmd.MaxAccountsPerIp),
        };

        // Every row is found before the first is written, so a missing row refuses the whole save.
        if (values.Keys.Any(key => !rows.ContainsKey(key)))
        {
            return Result.FailureCode(HostpanelErrorCodes.SETTING_NOT_FOUND);
        }

        foreach (var (key, value) in values)
        {
            var saved = await _bus.InvokeAsync<Result>(new UpdateSettingCommand(rows[key].Id, value), ct);
            if (saved.IsFailure)
            {
                return saved;
            }
        }

        return Result.Success();
    }

    /// <summary>Writes a number the way the settings store it.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The invariant-culture text.</returns>
    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
}
