namespace Innovayse.Application.Notifications.Commands.ToggleEmailTemplate;

using Innovayse.Application.Common;
using Innovayse.Domain.Notifications.Interfaces;

/// <summary>Activates or deactivates an email template.</summary>
public sealed class ToggleEmailTemplateHandler(IEmailTemplateRepository repo, IUnitOfWork uow)
{
    /// <summary>Handles <see cref="ToggleEmailTemplateCommand"/>.</summary>
    /// <param name="cmd">The toggle email template command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when no template with the given ID exists.</exception>
    public async Task HandleAsync(ToggleEmailTemplateCommand cmd, CancellationToken ct)
    {
        var template = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Email template {cmd.Id} was not found.");

        if (cmd.Active)
        {
            template.Activate();
        }
        else
        {
            template.Deactivate();
        }

        await uow.SaveChangesAsync(ct);
    }
}
