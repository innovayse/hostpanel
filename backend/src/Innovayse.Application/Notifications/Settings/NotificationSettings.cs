namespace Innovayse.Application.Notifications.Settings;

/// <summary>
/// Application-level notification settings, bound from the <c>Notifications</c> configuration section.
/// </summary>
public sealed class NotificationSettings
{
    /// <summary>Gets the email address to which admin/system alerts are sent.</summary>
    public required string AdminEmail { get; init; }
}
