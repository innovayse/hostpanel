namespace Innovayse.Hostpanel.Application.Campaigns.Common;

/// <summary>
/// How the delivery of an email campaign stands, counted over its recipients, and how the readers responded to the mails that
/// were delivered; all zero until sending starts.
/// </summary>
/// <param name="Total">Every recipient of the campaign.</param>
/// <param name="Pending">Recipients not delivered yet.</param>
/// <param name="Sent">Recipients the email reached.</param>
/// <param name="Failed">Recipients every allowed attempt failed for.</param>
/// <param name="Unsubscribed">Recipients skipped because they withdrew consent to marketing email.</param>
/// <param name="Opened">Every load of a mail's tracking pixel, repeated opens by one reader included.</param>
/// <param name="UniqueOpened">Delivered recipients who opened their mail at least once.</param>
/// <param name="Clicked">Every follow of a tracked link, repeated clicks by one reader included.</param>
/// <param name="UniqueClicked">Delivered recipients who followed a tracked link at least once.</param>
/// <param name="OpenRate">Share of the delivered mails that were opened at least once, from 0 to 1; 0 while nothing is delivered.</param>
/// <param name="ClickRate">Share of the delivered mails whose reader followed a tracked link at least once, from 0 to 1; 0 while nothing is delivered.</param>
public sealed record EmailCampaignStatsDto(
    int Total,
    int Pending,
    int Sent,
    int Failed,
    int Unsubscribed,
    int Opened,
    int UniqueOpened,
    int Clicked,
    int UniqueClicked,
    double OpenRate,
    double ClickRate);
