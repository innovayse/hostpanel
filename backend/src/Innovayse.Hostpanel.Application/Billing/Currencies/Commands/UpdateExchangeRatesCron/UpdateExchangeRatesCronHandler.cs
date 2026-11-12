using Innovayse.Hostpanel.Application.Billing.Currencies.Commands.UpdateExchangeRates;
using Innovayse.Hostpanel.Application.Billing.Currencies.Common;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Currencies.Commands.UpdateExchangeRatesCron;

/// <summary>
/// Handles <see cref="UpdateExchangeRatesCronCommand"/> by running the same update as the admin panel's button
/// (<see cref="UpdateExchangeRatesCommand"/>) and logging its outcome. A refusal never stops the job: the rates
/// stay as they were and the next run is scheduled all the same.
/// </summary>
public sealed class UpdateExchangeRatesCronHandler
{
    /// <summary>Runs the pass under its lease and schedules the next run.</summary>
    private readonly RecurringJobs _jobs;

    /// <summary>Message bus, to run the update command.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The scheduled jobs settings: the switch that turns the update off.</summary>
    private readonly IOptions<ScheduledJobsOptions> _options;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<UpdateExchangeRatesCronHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="jobs">Runs the pass under its lease and schedules the next run.</param>
    /// <param name="bus">Message bus, to run the update command.</param>
    /// <param name="options">The scheduled jobs settings: the switch that turns the update off.</param>
    /// <param name="logger">Structured logger.</param>
    public UpdateExchangeRatesCronHandler(
        RecurringJobs jobs,
        IMessageBus bus,
        IOptions<ScheduledJobsOptions> options,
        ILogger<UpdateExchangeRatesCronHandler> logger)
    {
        _jobs = jobs;
        _bus = bus;
        _options = options;
        _logger = logger;
    }

    /// <summary>Refreshes the rates and re-schedules itself for the next day.</summary>
    /// <param name="cmd">The scheduled command (marker, no payload).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the message has been handled.</returns>
    public Task HandleAsync(UpdateExchangeRatesCronCommand cmd, CancellationToken ct)
    {
        _ = cmd;
        return _jobs.RunAsync<UpdateExchangeRatesCronCommand>(
            _jobs.Schedules.UpdateExchangeRates, () => RunAsync(ct), ct);
    }

    /// <summary>One pass of the job; <see cref="HandleAsync"/> runs it through <see cref="RecurringJobs"/>.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the pass is over.</returns>
    private async Task RunAsync(CancellationToken ct)
    {
        if (_options.Value.DisableExchangeRateUpdates)
        {
            _logger.LogInformation("UpdateExchangeRatesCron: exchange rate updates are disabled; skipping.");
            return;
        }

        var outcome = await _bus.InvokeAsync<Result<UpdateExchangeRatesResultDto>>(new UpdateExchangeRatesCommand(), ct);
        if (outcome.IsFailure)
        {
            LogRefusal(outcome.ErrorCode!);
            return;
        }

        var result = outcome.Value!;
        _logger.LogInformation(
            "UpdateExchangeRatesCron: updated {UpdatedCount} rates ({Updated}); the source did not quote {MissingCount} ({Missing}).",
            result.Updated.Count, string.Join(", ", result.Updated), result.Missing.Count, string.Join(", ", result.Missing));
    }

    /// <summary>Logs why the update was refused: no base currency yet is expected, anything else is a warning.</summary>
    /// <param name="errorCode">The refusal code of the update.</param>
    private void LogRefusal(string errorCode)
    {
        if (errorCode == HostpanelErrorCodes.BASE_CURRENCY_NOT_CONFIGURED)
        {
            _logger.LogInformation("UpdateExchangeRatesCron: no base currency is configured yet; nothing to update.");
            return;
        }

        _logger.LogWarning(
            "UpdateExchangeRatesCron: the update was refused ({ErrorCode}); the rates are unchanged and the next run is scheduled.",
            errorCode);
    }
}
