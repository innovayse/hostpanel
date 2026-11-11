namespace Innovayse.Hostpanel.Application.Campaigns.Commands.DeleteEmailCampaign;

/// <summary>Command to delete a draft campaign.</summary>
/// <param name="Id">The identifier of the campaign to delete.</param>
public sealed record DeleteEmailCampaignCommand(int Id);
