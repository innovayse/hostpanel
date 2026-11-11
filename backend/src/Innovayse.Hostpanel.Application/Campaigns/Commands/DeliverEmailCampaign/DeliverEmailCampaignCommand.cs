namespace Innovayse.Hostpanel.Application.Campaigns.Commands.DeliverEmailCampaign;

/// <summary>
/// Command to send the next batch of mails of a campaign that is sending, and to finish the campaign once nobody is left
/// waiting. Sent by the recurring dispatch job, one message per campaign, and has no route of its own.
/// </summary>
/// <param name="Id">The identifier of the campaign to deliver.</param>
public sealed record DeliverEmailCampaignCommand(int Id);
