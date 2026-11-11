using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.DispatchEmailCampaignsCron;

/// <summary>
/// Scheduled command that moves email campaigns along: it starts the scheduled campaigns whose time has come and sends the
/// next batch of every campaign that is sending. Dispatched every <c>ScheduledJobsOptions.DispatchEmailCampaignsIntervalSeconds</c>
/// by <c>CampaignScheduledJobsStartup</c>.
/// </summary>
public sealed record DispatchEmailCampaignsCronCommand : IRecurringJob;
