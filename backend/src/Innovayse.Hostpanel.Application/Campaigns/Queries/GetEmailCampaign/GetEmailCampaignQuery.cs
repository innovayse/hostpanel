namespace Innovayse.Hostpanel.Application.Campaigns.Queries.GetEmailCampaign;

/// <summary>Query to retrieve one email campaign by id for the admin panel.</summary>
/// <param name="Id">The identifier of the campaign to retrieve.</param>
public sealed record GetEmailCampaignQuery(int Id);
