namespace Innovayse.Application.Notifications.Options;

/// <summary>
/// Application-level notification options -- where this platform's own operators are alerted.
/// Bound from the "Notifications" section in appsettings.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not validated at startup: <see cref="AdminEmail"/> is configured on no tier
/// today, so demanding the section would refuse to start every running deployment. The handler
/// that reads it (<c>DomainRegistrationFailedAdminHandler</c>) is reached only when a domain
/// registration fails, so an unset section is a feature that is not in use rather than a broken
/// process.
/// </para>
/// <para>
/// <see cref="ContactEmail"/> is reached far more often, so it is not left to fail silently:
/// <c>SendContactMessageHandler</c> refuses the submission and names this setting in the log
/// rather than reporting a message delivered to nobody. That is the shape
/// <c>options-and-configuration.md</c> asks for where a setting is required only by a feature a
/// deployment may not use -- throw where the feature is used, never fail open.
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
    /// deliberately no fallback between the two. They answer different questions -- this one is
    /// the enquiry inbox a person reads and replies from, <see cref="AdminEmail"/> is where
    /// automated operational alerts land -- and operators routinely route them to different
    /// mailboxes. Falling back would also make a value that is configured on no tier the
    /// load-bearing default for a path every visitor can reach, which is how a form starts
    /// delivering to nowhere again.
    /// <para>
    /// This is the setting that replaced the public site's own <c>EMAIL_TO</c>. It moved because
    /// the frontend held the SMTP credentials to go with it, in a container that had no other
    /// reason to hold them.
    /// </para>
    /// </remarks>
    public required string ContactEmail { get; init; }
}
