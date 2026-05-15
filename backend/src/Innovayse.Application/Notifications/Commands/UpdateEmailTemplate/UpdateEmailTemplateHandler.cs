namespace Innovayse.Application.Notifications.Commands.UpdateEmailTemplate;

using Innovayse.Application.Common;
using Innovayse.Domain.Notifications.Interfaces;

/// <summary>Updates the content of an existing email template.</summary>
public sealed class UpdateEmailTemplateHandler(IEmailTemplateRepository repo, IUnitOfWork uow)
{
    /// <summary>Handles <see cref="UpdateEmailTemplateCommand"/>.</summary>
    /// <param name="cmd">The update email template command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when no template with the given ID exists.</exception>
    public async Task HandleAsync(UpdateEmailTemplateCommand cmd, CancellationToken ct)
    {
        var template = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Email template {cmd.Id} was not found.");

        template.Update(cmd.Subject, cmd.Body, cmd.Description);
        await uow.SaveChangesAsync(ct);
    }
}
