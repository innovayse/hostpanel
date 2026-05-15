namespace Innovayse.API.Notifications.Requests;

/// <summary>Request body for toggling the active state of an email template.</summary>
public sealed class ToggleEmailTemplateRequest
{
    /// <summary>Gets a value indicating whether the template should be active.</summary>
    public required bool Active { get; init; }
}
