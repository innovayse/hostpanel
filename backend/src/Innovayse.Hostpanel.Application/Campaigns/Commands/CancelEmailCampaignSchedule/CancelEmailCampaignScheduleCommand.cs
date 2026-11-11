namespace Innovayse.Hostpanel.Application.Campaigns.Commands.CancelEmailCampaignSchedule;

/// <summary>Command to take a scheduled campaign back to a draft.</summary>
/// <param name="Id">The identifier of the campaign whose schedule is cancelled.</param>
public sealed record CancelEmailCampaignScheduleCommand(int Id);
