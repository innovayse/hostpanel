using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.Services.Commands.ProcessTrialsCron;

/// <summary>
/// Scheduled command that takes the daily step of every running free trial: reminders, conversion or termination.
/// Dispatched daily by <c>BillingScheduledJobsStartup</c> at the hour set in <c>ScheduledJobsOptions</c>.
/// </summary>
public sealed record ProcessTrialsCronCommand : IRecurringJob;
