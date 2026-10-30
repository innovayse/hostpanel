using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.Admin.Commands.CheckReleaseUpdatesCron;

/// <summary>
/// Scheduled command that queries the GitHub Releases API for the latest published Hostpanel
/// release and caches the result in the database. Dispatched daily by
/// <c>AdminScheduledJobsStartup</c> at the hour set in <c>ScheduledJobsOptions</c>.
/// </summary>
public sealed record CheckReleaseUpdatesCronCommand : IRecurringJob;
