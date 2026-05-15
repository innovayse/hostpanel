namespace Innovayse.Application.Domains.Commands.AddDnsRecord;

using Innovayse.Application.Common;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Adds a new DNS record to a domain's zone both in the aggregate and at the registrar.
/// The record is added to the aggregate first so its generated ID is available when calling the registrar.
/// </summary>
public sealed class AddDnsRecordHandler(
    IRegistrarProvider registrar,
    IDomainRepository repo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="AddDnsRecordCommand"/>.
    /// </summary>
    /// <param name="cmd">The add DNS record command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain is not found or the registrar rejects the record addition.
    /// </exception>
    public async Task HandleAsync(AddDnsRecordCommand cmd, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(cmd.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain {cmd.DomainId} not found.");

        domain.AddDnsRecord(cmd.Type, cmd.Host, cmd.Value, cmd.Ttl, cmd.Priority);

        if (domain.RegistrarRef is not null)
        {
            var newRecord = domain.DnsRecords[^1];
            var result = await registrar.AddDnsRecordAsync(domain.Name, domain.RegistrarRef, newRecord, ct);

            if (!result.Success)
            {
                throw new InvalidOperationException(
                    $"Registrar failed to add DNS record for '{domain.Name}': {result.ErrorMessage}");
            }
        }

        await uow.SaveChangesAsync(ct);
    }
}
