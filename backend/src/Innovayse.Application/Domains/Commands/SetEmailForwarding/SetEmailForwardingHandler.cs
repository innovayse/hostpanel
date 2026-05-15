namespace Innovayse.Application.Domains.Commands.SetEmailForwarding;

using Innovayse.Application.Common;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Enables or disables email forwarding for a domain at both the registrar and aggregate levels.
/// </summary>
public sealed class SetEmailForwardingHandler(
    IRegistrarProvider registrar,
    IDomainRepository repo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="SetEmailForwardingCommand"/>.
    /// </summary>
    /// <param name="cmd">The set email forwarding command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain is not found or the registrar rejects the update.
    /// </exception>
    public async Task HandleAsync(SetEmailForwardingCommand cmd, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(cmd.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain {cmd.DomainId} not found.");

        var result = await registrar.SetEmailForwardingAsync(domain.Name, cmd.Enabled, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Registrar failed to update email forwarding for '{domain.Name}': {result.ErrorMessage}");
        }

        domain.SetEmailForwarding(cmd.Enabled);
        await uow.SaveChangesAsync(ct);
    }
}
