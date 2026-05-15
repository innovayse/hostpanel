namespace Innovayse.Application.Domains.Commands.SetRegistrarLock;

using Innovayse.Application.Common;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Enables or disables the registrar transfer-lock for a domain at both the registrar and aggregate levels.
/// </summary>
public sealed class SetRegistrarLockHandler(
    IRegistrarProvider registrar,
    IDomainRepository repo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="SetRegistrarLockCommand"/>.
    /// </summary>
    /// <param name="cmd">The set registrar lock command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain is not found or the registrar rejects the update.
    /// </exception>
    public async Task HandleAsync(SetRegistrarLockCommand cmd, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(cmd.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain {cmd.DomainId} not found.");

        if (domain.RegistrarRef is not null)
        {
            var result = await registrar.SetRegistrarLockAsync(domain.Name, domain.RegistrarRef, cmd.Value, ct);

            if (!result.Success)
            {
                throw new InvalidOperationException(
                    $"Registrar failed to update lock status for '{domain.Name}': {result.ErrorMessage}");
            }
        }

        domain.SetLock(cmd.Value);
        await uow.SaveChangesAsync(ct);
    }
}
