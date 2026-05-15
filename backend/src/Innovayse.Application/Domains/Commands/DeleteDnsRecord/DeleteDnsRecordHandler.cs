namespace Innovayse.Application.Domains.Commands.DeleteDnsRecord;

using Innovayse.Application.Common;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Removes a DNS record from the domain aggregate and deletes it at the registrar.
/// </summary>
public sealed class DeleteDnsRecordHandler(
    IRegistrarProvider registrar,
    IDomainRepository repo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="DeleteDnsRecordCommand"/>.
    /// </summary>
    /// <param name="cmd">The delete DNS record command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain or record is not found, or the registrar rejects the deletion.
    /// </exception>
    public async Task HandleAsync(DeleteDnsRecordCommand cmd, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(cmd.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain {cmd.DomainId} not found.");

        domain.RemoveDnsRecord(cmd.RecordId);

        if (domain.RegistrarRef is not null)
        {
            var result = await registrar.DeleteDnsRecordAsync(domain.Name, domain.RegistrarRef, cmd.RecordId, ct);

            if (!result.Success)
            {
                throw new InvalidOperationException(
                    $"Registrar failed to delete DNS record {cmd.RecordId} for '{domain.Name}': {result.ErrorMessage}");
            }
        }

        await uow.SaveChangesAsync(ct);
    }
}
