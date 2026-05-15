namespace Innovayse.Application.Notifications.Commands.SendEmail;

using Innovayse.Application.Common;
using Innovayse.Application.Notifications.Services;
using Innovayse.Domain.Notifications;
using Innovayse.Domain.Notifications.Interfaces;

/// <summary>
/// Sends an email by rendering the requested template and delegating to <see cref="IEmailSender"/>.
/// Always writes an <see cref="EmailLog"/> entry regardless of send outcome.
/// </summary>
public sealed class SendEmailHandler(
    IEmailTemplateRepository templateRepo,
    IEmailLogRepository logRepo,
    IEmailSender emailSender,
    IUnitOfWork uow,
    TemplateRenderer renderer)
{
    /// <summary>Handles <see cref="SendEmailCommand"/>.</summary>
    /// <param name="cmd">The send email command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the requested template slug does not exist.</exception>
    public async Task HandleAsync(SendEmailCommand cmd, CancellationToken ct)
    {
        var template = await templateRepo.FindBySlugAsync(cmd.TemplateSlug, ct)
            ?? throw new InvalidOperationException($"Email template '{cmd.TemplateSlug}' was not found.");

        if (!template.IsActive)
        {
            return;
        }

        var subject = await renderer.RenderAsync(template.Subject, cmd.TemplateData);
        var body = await renderer.RenderAsync(template.Body, cmd.TemplateData);

        string? sendError = null;
        var success = false;

        try
        {
            await emailSender.SendAsync(new EmailMessage(cmd.To, subject, body), ct);
            success = true;
        }
        catch (Exception ex)
        {
            sendError = ex.Message;
        }

        var log = EmailLog.Create(cmd.To, subject, body, success, sendError);
        logRepo.Add(log);
        await uow.SaveChangesAsync(ct);
    }
}
