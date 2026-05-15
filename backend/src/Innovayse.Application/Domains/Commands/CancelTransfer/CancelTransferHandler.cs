namespace Innovayse.Application.Domains.Commands.CancelTransfer;

using Innovayse.Application.Common;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Cancels a pending incoming domain transfer at the registrar and marks the domain aggregate as cancelled.
/// </summary>
public sealed class CancelTransferHandler(
    IRegistrarProvider registrar,
    IDomainRepository repo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="CancelTransferCommand"/>.
    /// </summary>
    /// <param name="cmd">The cancel transfer command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain is not found or the registrar rejects the cancellation.
    /// </exception>
    public async Task HandleAsync(CancelTransferCommand cmd, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(cmd.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain {cmd.DomainId} not found.");

        var result = await registrar.CancelTransferAsync(domain.Name, domain.RegistrarRef ?? string.Empty, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Registrar rejected transfer cancellation for '{domain.Name}': {result.ErrorMessage}");
        }

        domain.Cancel();
        await uow.SaveChangesAsync(ct);
    }
}
