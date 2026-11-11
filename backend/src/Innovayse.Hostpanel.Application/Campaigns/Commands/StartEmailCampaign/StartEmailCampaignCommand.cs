namespace Innovayse.Hostpanel.Application.Campaigns.Commands.StartEmailCampaign;

/// <summary>
/// Command to start sending a scheduled campaign whose time has come: fixes who receives it. Sent by the recurring
/// dispatch job, one message per campaign, and has no route of its own.
/// </summary>
/// <param name="Id">The identifier of the campaign to start.</param>
public sealed record StartEmailCampaignCommand(int Id);
