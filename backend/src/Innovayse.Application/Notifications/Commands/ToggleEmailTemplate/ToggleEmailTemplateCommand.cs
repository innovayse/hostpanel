namespace Innovayse.Application.Notifications.Commands.ToggleEmailTemplate;

/// <summary>Command to activate or deactivate an email template.</summary>
/// <param name="Id">The identifier of the template to toggle.</param>
/// <param name="Active">Pass <see langword="true"/> to activate, <see langword="false"/> to deactivate.</param>
public record ToggleEmailTemplateCommand(int Id, bool Active);
