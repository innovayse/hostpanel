namespace Innovayse.Application.Notifications.Commands.DeleteEmailTemplate;

/// <summary>Command to permanently remove an email template.</summary>
/// <param name="Id">The identifier of the template to delete.</param>
public record DeleteEmailTemplateCommand(int Id);
