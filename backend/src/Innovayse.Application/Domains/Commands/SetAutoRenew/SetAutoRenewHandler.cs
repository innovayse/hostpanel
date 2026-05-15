namespace Innovayse.Application.Domains.Commands.SetAutoRenew;

using Innovayse.Application.Common;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Enables or disables automatic renewal for a domain at both the registrar and aggregate levels.
/// </summary>
public sealed class SetAutoRenewHandler(
    IRegistrarProvider registrar,
    IDomainRepository repo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="SetAutoRenewCommand"/>.
    /// </summary>
    /// <param name="cmd">The set auto-renew command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain is not found or the registrar rejects the update.
    /// </exception>
    public async Task HandleAsync(SetAutoRenewCommand cmd, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(cmd.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain {cmd.DomainId} not found.");

        if (domain.RegistrarRef is not null)
        {
            var result = await registrar.SetAutoRenewAsync(domain.Name, domain.RegistrarRef, cmd.Value, ct);

            if (!result.Success)
            {
                throw new InvalidOperationException(
                    $"Registrar failed to update auto-renew for '{domain.Name}': {result.ErrorMessage}");
            }
        }

        domain.SetAutoRenew(cmd.Value);
        await uow.SaveChangesAsync(ct);
    }
}
