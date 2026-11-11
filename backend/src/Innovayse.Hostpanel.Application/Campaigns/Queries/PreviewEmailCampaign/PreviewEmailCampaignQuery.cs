namespace Innovayse.Hostpanel.Application.Campaigns.Queries.PreviewEmailCampaign;

/// <summary>Query to render a saved campaign the way its recipients would see it.</summary>
/// <param name="Id">The identifier of the campaign to render.</param>
public sealed record PreviewEmailCampaignQuery(int Id);
