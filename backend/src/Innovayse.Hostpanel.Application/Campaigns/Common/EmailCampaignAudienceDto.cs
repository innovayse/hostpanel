namespace Innovayse.Hostpanel.Application.Campaigns.Common;

/// <summary>The size of the audience a set of campaign segments reaches.</summary>
/// <param name="Count">Number of clients who agreed to marketing email, match at least one segment and have a sign-in address, one client per address.</param>
public sealed record EmailCampaignAudienceDto(int Count);
