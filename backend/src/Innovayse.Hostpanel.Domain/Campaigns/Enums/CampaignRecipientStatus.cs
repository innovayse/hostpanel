namespace Innovayse.Hostpanel.Domain.Campaigns.Enums;

/// <summary>How the delivery to one campaign recipient stands.</summary>
public enum CampaignRecipientStatus
{
    /// <summary>Not delivered yet, either never tried or tried and due for another attempt.</summary>
    Pending = 0,

    /// <summary>Delivered.</summary>
    Sent = 1,

    /// <summary>Every allowed attempt failed; no more are made.</summary>
    Failed = 2,

    /// <summary>Skipped because the client withdrew consent to marketing email.</summary>
    Unsubscribed = 3
}
