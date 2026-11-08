using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.EnrollRegistryInstanceCron;

/// <summary>
/// Scheduled command that registers this installation with the plugin registry and verifies it, and tries again after a failure
/// once its back-off has run out. Scheduled on startup by <c>AdminScheduledJobsStartup</c> and by its own handler after every pass.
/// </summary>
public sealed record EnrollRegistryInstanceCronCommand : IRecurringJob;
