namespace Innovayse.API.Notifications.Requests;

/// <summary>Request body for creating a new email template.</summary>
public sealed class CreateEmailTemplateRequest
{
    /// <summary>Gets the unique slug used to look up this template.</summary>
    public required string Slug { get; init; }

    /// <summary>Gets the email subject line, which may contain Liquid template variables.</summary>
    public required string Subject { get; init; }

    /// <summary>Gets the email body content, which may contain Liquid template variables.</summary>
    public required string Body { get; init; }

    /// <summary>Gets an optional human-readable description of when this template is used.</summary>
    public string? Description { get; init; }
}
