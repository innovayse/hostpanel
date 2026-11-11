namespace Innovayse.Hostpanel.Application.Campaigns.Common;

/// <summary>How the delivery of an email campaign stands, counted over its recipients; all zero until sending starts.</summary>
/// <param name="Total">Every recipient of the campaign.</param>
/// <param name="Pending">Recipients not delivered yet.</param>
/// <param name="Sent">Recipients the email reached.</param>
/// <param name="Failed">Recipients every allowed attempt failed for.</param>
/// <param name="Unsubscribed">Recipients skipped because they withdrew consent to marketing email.</param>
public sealed record EmailCampaignStatsDto(int Total, int Pending, int Sent, int Failed, int Unsubscribed);
