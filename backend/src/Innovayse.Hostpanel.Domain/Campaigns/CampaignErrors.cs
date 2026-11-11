namespace Innovayse.Hostpanel.Domain.Campaigns;

/// <summary>
/// Refusal codes the <see cref="EmailCampaign"/> aggregate and its children throw as
/// <see cref="Innovayse.Hostpanel.Domain.DomainRuleException"/>.
/// </summary>
/// <remarks>
/// Declared here, not in <c>Application.Common.Errors.HostpanelErrorCodes</c>, because Domain may
/// not reference Application; <c>HostpanelErrorCodes</c> re-declares the same string values from
/// these constants so the resource coverage test still requires a sentence for each one.
/// </remarks>
public static class CampaignErrors
{
    /// <summary>The campaign is not a draft, so it cannot be edited, scheduled or deleted (409).</summary>
    public const string NotDraft = "CAMPAIGN_NOT_DRAFT";

    /// <summary>The campaign is not scheduled, so its schedule cannot be cancelled and it cannot start sending (409).</summary>
    public const string NotScheduled = "CAMPAIGN_NOT_SCHEDULED";

    /// <summary>The campaign is not being sent, so it cannot gain recipients or be finished (409).</summary>
    public const string NotSending = "CAMPAIGN_NOT_SENDING";

    /// <summary>The campaign has no segment, so it would reach nobody and cannot be scheduled (400).</summary>
    public const string NoSegment = "CAMPAIGN_NO_SEGMENT";

    /// <summary>The moment to send at is not in the future (400).</summary>
    public const string ScheduleInPast = "CAMPAIGN_SCHEDULE_IN_PAST";

    /// <summary>A segment's value is missing, is given where the filter takes none, or does not fit the filter (400).</summary>
    public const string SegmentValueInvalid = "CAMPAIGN_SEGMENT_VALUE_INVALID";

    /// <summary>The recipient is not waiting for delivery, so its outcome cannot be recorded again (409).</summary>
    public const string RecipientNotPending = "CAMPAIGN_RECIPIENT_NOT_PENDING";
}
