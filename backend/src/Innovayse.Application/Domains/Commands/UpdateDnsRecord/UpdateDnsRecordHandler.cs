namespace Innovayse.Application.Domains.Commands.UpdateDnsRecord;

using Innovayse.Application.Common;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Updates an existing DNS record in the domain aggregate and syncs the change to the registrar.
/// </summary>
public sealed class UpdateDnsRecordHandler(
    IRegistrarProvider registrar,
    IDomainRepository repo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="UpdateDnsRecordCommand"/>.
    /// </summary>
    /// <param name="cmd">The update DNS record command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain or record is not found, or the registrar rejects the update.
    /// </exception>
    public async Task HandleAsync(UpdateDnsRecordCommand cmd, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(cmd.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain {cmd.DomainId} not found.");

        domain.UpdateDnsRecord(cmd.RecordId, cmd.Value, cmd.Ttl, cmd.Priority);

        if (domain.RegistrarRef is not null)
        {
            var updated = domain.DnsRecords.First(r => r.Id == cmd.RecordId);
            var result = await registrar.UpdateDnsRecordAsync(domain.Name, domain.RegistrarRef, updated, ct);

            if (!result.Success)
            {
                throw new InvalidOperationException(
                    $"Registrar failed to update DNS record {cmd.RecordId} for '{domain.Name}': {result.ErrorMessage}");
            }
        }

        await uow.SaveChangesAsync(ct);
    }
}
