namespace Innovayse.Hostpanel.Domain.Billing.Enums;

/// <summary>Where the delivery of one <see cref="FulfilmentRecord"/> stands.</summary>
public enum FulfilmentStatus
{
    /// <summary>Waiting to be delivered: never tried yet, or waiting out the back-off after a failed attempt.</summary>
    Pending = 0,

    /// <summary>Claimed by a dispatcher that is calling the plugin now, until its lease ends.</summary>
    Processing = 1,

    /// <summary>The plugin accepted it; nothing more happens to the record.</summary>
    Done = 2,

    /// <summary>Every attempt failed; it waits for staff to retry it.</summary>
    Failed = 3,
}
