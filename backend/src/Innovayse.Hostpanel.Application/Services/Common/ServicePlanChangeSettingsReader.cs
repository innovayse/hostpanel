using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Queries.GetSettingValues;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>
/// Reads the plan change switch of <see cref="BillingSettingKeys"/>. A missing or unparsable row falls back to the
/// seeded default with a warning: a typo in System Settings must not approve downgrades nobody meant to approve.
/// The settings belong to the Admin module, so the row is read through its <see cref="GetSettingValuesQuery"/>.
/// </summary>
public sealed class ServicePlanChangeSettingsReader
{
    /// <summary>Dispatches the Admin settings query.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Warns about a fallback.</summary>
    private readonly ILogger<ServicePlanChangeSettingsReader> _logger;

    /// <summary>Initializes the reader.</summary>
    /// <param name="bus">Dispatches the Admin settings query.</param>
    /// <param name="logger">Warns about a fallback.</param>
    public ServicePlanChangeSettingsReader(IMessageBus bus, ILogger<ServicePlanChangeSettingsReader> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    /// <summary>Reads whether a client's downgrade is approved at once.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when downgrades are approved without staff; the default is <see langword="false"/>.</returns>
    public async Task<bool> AutoApproveDowngradesAsync(CancellationToken ct)
    {
        var key = BillingSettingKeys.AutoApproveDowngrades;
        var result = await _bus.InvokeAsync<Result<IReadOnlyList<SettingDto>>>(new GetSettingValuesQuery([key]), ct);
        var stored = (result.Value ?? []).FirstOrDefault(s => s.Key == key)?.Value;

        if (stored is not null && BillingSettingKeys.Patterns[key].Regex.IsMatch(stored))
        {
            return bool.Parse(stored);
        }

        _logger.LogWarning("Billing setting {Key} is missing or invalid; using its default.", key);
        return bool.Parse(BillingSettingKeys.Defaults.First(d => d.Key == key).Value);
    }
}
