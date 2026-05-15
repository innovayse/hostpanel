namespace Innovayse.Application.Notifications.Commands.DeleteEmailTemplate;

using Innovayse.Application.Common;
using Innovayse.Domain.Notifications.Interfaces;

/// <summary>Permanently removes an email template from the store.</summary>
public sealed class DeleteEmailTemplateHandler(IEmailTemplateRepository repo, IUnitOfWork uow)
{
    /// <summary>Handles <see cref="DeleteEmailTemplateCommand"/>.</summary>
    /// <param name="cmd">The delete email template command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when no template with the given ID exists.</exception>
    public async Task HandleAsync(DeleteEmailTemplateCommand cmd, CancellationToken ct)
    {
        var template = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Email template {cmd.Id} was not found.");

        repo.Remove(template);
        await uow.SaveChangesAsync(ct);
    }
}
