namespace Innovayse.Application.Notifications.Commands.SendEmail;

/// <summary>Command to send an email using a registered template.</summary>
/// <param name="To">The recipient email address.</param>
/// <param name="TemplateSlug">The unique slug of the email template to use.</param>
/// <param name="TemplateData">The data model passed to the Liquid template renderer.</param>
public record SendEmailCommand(string To, string TemplateSlug, object TemplateData);
