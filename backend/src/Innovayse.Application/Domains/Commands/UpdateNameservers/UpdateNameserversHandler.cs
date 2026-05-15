namespace Innovayse.Application.Domains.Commands.UpdateNameservers;

using Innovayse.Application.Common;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Replaces the nameserver list for a domain at both the registrar and aggregate levels.
/// </summary>
public sealed class UpdateNameserversHandler(
    IRegistrarProvider registrar,
    IDomainRepository repo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="UpdateNameserversCommand"/>.
    /// </summary>
    /// <param name="cmd">The update nameservers command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain is not found or the registrar rejects the update.
    /// </exception>
    public async Task HandleAsync(UpdateNameserversCommand cmd, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(cmd.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain {cmd.DomainId} not found.");

        if (domain.RegistrarRef is not null)
        {
            var result = await registrar.SetNameserversAsync(domain.Name, domain.RegistrarRef, cmd.Nameservers, ct);

            if (!result.Success)
            {
                throw new InvalidOperationException(
                    $"Registrar failed to update nameservers for '{domain.Name}': {result.ErrorMessage}");
            }
        }

        domain.SetNameservers(cmd.Nameservers);
        await uow.SaveChangesAsync(ct);
    }
}
