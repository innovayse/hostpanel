namespace Innovayse.Domain.Notifications;

using Innovayse.Domain.Common;

/// <summary>
/// Represents an email template used to generate outbound emails.
/// Templates are identified by a unique slug and contain Liquid-syntax subject and body.
/// </summary>
public sealed class EmailTemplate : AggregateRoot
{
    /// <summary>Gets the unique identifier slug used to look up this template.</summary>
    public string Slug { get; private set; } = string.Empty;

    /// <summary>Gets the email subject line, which may contain Liquid template variables.</summary>
    public string Subject { get; private set; } = string.Empty;

    /// <summary>Gets the email body content, which may contain Liquid template variables.</summary>
    public string Body { get; private set; } = string.Empty;

    /// <summary>Gets an optional human-readable description of when this template is used.</summary>
    public string? Description { get; private set; }

    /// <summary>Gets a value indicating whether this template is active and can be used for sending.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Initialises a new <see cref="EmailTemplate"/> for EF Core materialisation.</summary>
    internal EmailTemplate() : base(0)
    {
    }

    /// <summary>Initialises an <see cref="EmailTemplate"/> with the given identity.</summary>
    /// <param name="id">The entity identifier.</param>
    private EmailTemplate(int id) : base(id)
    {
    }

    /// <summary>
    /// Creates a new active email template.
    /// </summary>
    /// <param name="slug">Unique slug used to look up the template.</param>
    /// <param name="subject">Email subject line, may include Liquid variables.</param>
    /// <param name="body">Email body, may include Liquid variables.</param>
    /// <param name="description">Optional description of the template's purpose.</param>
    /// <returns>A new <see cref="EmailTemplate"/> with <see cref="IsActive"/> set to <see langword="true"/>.</returns>
    public static EmailTemplate Create(string slug, string subject, string body, string? description)
    {
        var template = new EmailTemplate(0)
        {
            Slug = slug,
            Subject = subject,
            Body = body,
            Description = description,
            IsActive = true,
        };

        return template;
    }

    /// <summary>
    /// Updates the subject, body, and description of this template.
    /// </summary>
    /// <param name="subject">New email subject line.</param>
    /// <param name="body">New email body.</param>
    /// <param name="description">New optional description.</param>
    public void Update(string subject, string body, string? description)
    {
        Subject = subject;
        Body = body;
        Description = description;
    }

    /// <summary>Marks this template as active, allowing it to be used for sending emails.</summary>
    public void Activate() => IsActive = true;

    /// <summary>Marks this template as inactive, preventing it from being used for sending emails.</summary>
    public void Deactivate() => IsActive = false;
}
