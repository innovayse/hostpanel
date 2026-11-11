namespace Innovayse.Hostpanel.Application.Notifications.Queries.FindEmailTemplate;

/// <summary>Query to find the stored version of an email template that a mail to a recipient is rendered from.</summary>
/// <param name="Slug">The unique slug of the template.</param>
/// <param name="Language">The recipient's language; the version in it is used when one exists, the default language's otherwise.</param>
public sealed record FindEmailTemplateQuery(string Slug, string? Language);
