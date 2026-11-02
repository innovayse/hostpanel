using System.ComponentModel.DataAnnotations;

namespace Innovayse.Hostpanel.Application.Common.Settings;

/// <summary>
/// Application-level notification options: where this platform's own operators are alerted.
/// Bound from the "Notifications" section in appsettings.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not validated at startup: <see cref="AdminEmail"/> is optional and commonly
/// unset, so demanding the section would refuse to start such a deployment. The handler
/// that reads it (<c>DomainRegistrationFailedAdminHandler</c>) is reached only when a domain
/// registration fails, so an unset section is a feature that is not in use rather than a broken
/// process.
/// </para>
/// <para>
/// <see cref="ContactEmail"/> is reached far more often, so it is not left to fail silently:
/// <c>SendContactMessageHandler</c> refuses the submission and names this setting in the log
/// rather than reporting a message delivered to nobody: a setting required only by a feature a
/// deployment may not use fails where the feature is used, never open.
/// </para>
/// </remarks>
public sealed class NotificationOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Notifications";

    /// <summary>Gets the email address to which admin/system alerts are sent.</summary>
    public required string AdminEmail { get; init; }

    /// <summary>
    /// Gets the email address the public website's contact form delivers to.
    /// </summary>
    /// <remarks>
    /// A separate setting from <see cref="AdminEmail"/> rather than a reuse of it, and
    /// deliberately no fallback between the two, with one exception: renewal alerts
    /// (<c>DomainRenewalFailedEvent</c>) go to <see cref="AdminEmail"/> and fall back to this
    /// address, because <see cref="AdminEmail"/> is commonly unset and a failed domain
    /// renewal must still reach a person. They answer different questions, this one is
    /// the enquiry inbox a person reads and replies from, <see cref="AdminEmail"/> is where
    /// automated operational alerts land, and operators routinely route them to different
    /// mailboxes. Falling back would also make a commonly unset value the load-bearing default
    /// for a path every visitor can reach, and the form would deliver to nowhere.
    /// <para>
    /// This is the setting that replaced the public site's own <c>EMAIL_TO</c>. It moved because
    /// the frontend held the SMTP credentials to go with it, in a container that had no other
    /// reason to hold them.
    /// </para>
    /// </remarks>
    public required string ContactEmail { get; init; }

    /// <summary>
    /// How long a keyed mail claimed for sending (state Sending) is trusted to be in flight, in seconds,
    /// 30 to 3600. A replay inside this window skips the recipient; after it, the sender is presumed to
    /// have crashed between claim and outcome and the mail is sent again.
    /// </summary>
    [Range(30, 3600)]
    public int DeliveryClaimSeconds { get; init; } = 300;

    /// <summary>The claim lease as a <see cref="TimeSpan"/>, so callers do not repeat the unit.</summary>
    public TimeSpan DeliveryClaimLease => TimeSpan.FromSeconds(DeliveryClaimSeconds);
}
