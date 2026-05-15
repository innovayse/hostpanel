namespace Innovayse.Application.Notifications.Commands.UpdateEmailTemplate;

/// <summary>Command to update the content of an existing email template.</summary>
/// <param name="Id">The identifier of the template to update.</param>
/// <param name="Subject">New email subject line.</param>
/// <param name="Body">New email body content.</param>
/// <param name="Description">New optional description.</param>
public record UpdateEmailTemplateCommand(int Id, string Subject, string Body, string? Description);
