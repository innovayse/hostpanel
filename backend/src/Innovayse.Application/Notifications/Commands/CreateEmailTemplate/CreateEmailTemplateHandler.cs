namespace Innovayse.Application.Notifications.Commands.CreateEmailTemplate;

using Innovayse.Application.Common;
using Innovayse.Domain.Notifications;
using Innovayse.Domain.Notifications.Interfaces;

/// <summary>Creates a new email template and persists it.</summary>
public sealed class CreateEmailTemplateHandler(IEmailTemplateRepository repo, IUnitOfWork uow)
{
    /// <summary>Handles <see cref="CreateEmailTemplateCommand"/>.</summary>
    /// <param name="cmd">The create email template command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created template ID.</returns>
    public async Task<int> HandleAsync(CreateEmailTemplateCommand cmd, CancellationToken ct)
    {
        var template = EmailTemplate.Create(cmd.Slug, cmd.Subject, cmd.Body, cmd.Description);
        repo.Add(template);
        await uow.SaveChangesAsync(ct);
        return template.Id;
    }
}
