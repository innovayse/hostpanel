namespace Innovayse.API.Notifications.Requests;

/// <summary>Request body for updating an existing email template's content.</summary>
public sealed class UpdateEmailTemplateRequest
{
    /// <summary>Gets the new email subject line, which may contain Liquid template variables.</summary>
    public required string Subject { get; init; }

    /// <summary>Gets the new email body content, which may contain Liquid template variables.</summary>
    public required string Body { get; init; }

    /// <summary>Gets the new optional description of when this template is used.</summary>
    public string? Description { get; init; }
}
