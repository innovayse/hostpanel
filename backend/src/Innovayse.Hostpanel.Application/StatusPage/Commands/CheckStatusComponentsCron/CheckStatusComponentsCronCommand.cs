using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.CheckStatusComponentsCron;

/// <summary>
/// Scheduled command to check every monitored status page component. Scheduled on startup by
/// <c>StatusScheduledJobsStartup</c> and by its own handler after every pass, at the interval set in <c>ScheduledJobsOptions</c>.
/// </summary>
public sealed record CheckStatusComponentsCronCommand : IRecurringJob;
