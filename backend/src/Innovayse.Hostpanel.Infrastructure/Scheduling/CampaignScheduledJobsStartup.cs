using Innovayse.Hostpanel.Application.Campaigns.Commands.DispatchEmailCampaignsCron;
using Innovayse.Hostpanel.Application.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Innovayse.Hostpanel.Infrastructure.Scheduling;

/// <summary>
/// Hosted service that starts the email campaign dispatch on application startup. Its interval is the
/// <c>ScheduledJobsOptions</c> value the job handler reads too, so the schedule is stated once.
/// </summary>
/// <remarks>
/// <see cref="RecurringJobs.StartAsync{TCommand}"/> schedules the job for its next regular run, or right now when the job
/// missed its last one (the application was down). The handler schedules every later run itself.
/// </remarks>
public sealed class CampaignScheduledJobsStartup : IHostedService
{
    /// <summary>Opens the scope each startup run resolves its services in.</summary>
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Initializes a new instance of the <see cref="CampaignScheduledJobsStartup"/> class.</summary>
    /// <param name="scopeFactory">Opens the scope each startup run resolves its services in.</param>
    public CampaignScheduledJobsStartup(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>Starts the email campaign dispatch.</summary>
    /// <param name="cancellationToken">Cancellation token provided by the host.</param>
    /// <returns>A <see cref="Task"/> that completes once the first run of the job is scheduled.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var jobs = scope.ServiceProvider.GetRequiredService<RecurringJobs>();

        await jobs.StartAsync<DispatchEmailCampaignsCronCommand>(jobs.Schedules.DispatchEmailCampaigns, cancellationToken);
    }

    /// <summary>
    /// No-op: nothing needs cleanup on shutdown. A run scheduled in memory is lost with the process, and the next start
    /// catches the job up from its last recorded success.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token provided by the host.</param>
    /// <returns>A completed <see cref="Task"/>.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
