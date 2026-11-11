namespace Innovayse.Hostpanel.Domain.Campaigns.Enums;

/// <summary>Where an email campaign is in its life: written, queued for a moment, being delivered, or finished.</summary>
public enum CampaignStatus
{
    /// <summary>Being written; the only state in which the campaign can be edited or deleted.</summary>
    Draft = 0,

    /// <summary>Queued to start sending at its scheduled moment.</summary>
    Scheduled = 1,

    /// <summary>Its recipients are being delivered to.</summary>
    Sending = 2,

    /// <summary>Delivery has finished.</summary>
    Sent = 3
}
