namespace Innovayse.Application.Notifications.Commands.CreateEmailTemplate;

/// <summary>Command to create a new email template.</summary>
/// <param name="Slug">Unique slug used to look up the template.</param>
/// <param name="Subject">Email subject line, may include Liquid variables.</param>
/// <param name="Body">Email body, may include Liquid variables.</param>
/// <param name="Description">Optional human-readable description of the template's purpose.</param>
public record CreateEmailTemplateCommand(string Slug, string Subject, string Body, string? Description);
