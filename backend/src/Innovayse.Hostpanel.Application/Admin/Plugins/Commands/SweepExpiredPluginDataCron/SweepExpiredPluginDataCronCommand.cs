using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.SweepExpiredPluginDataCron;

/// <summary>
/// Scheduled command that removes the plugin store values whose time to live has passed and releases them from their
/// scope's totals. Scheduled on startup by <c>AdminScheduledJobsStartup</c> and by its own handler after every pass.
/// </summary>
public sealed record SweepExpiredPluginDataCronCommand : IRecurringJob;
