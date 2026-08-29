namespace Innovayse.Application.Domains.Commands.DeleteMyDomainDnsRecord;

using Innovayse.Application.Domains.Commands.DeleteDnsRecord;
using Innovayse.Application.Domains.Common;
using Wolverine;

/// <summary>
/// Deletes a DNS record from one of the calling client's own domains, refusing every domain that is
/// not theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// remove a zone entry through <see cref="DeleteMyDomainDnsRecordCommand"/> without it having run.
/// Once ownership is settled the work is the same the admin route performs, so this dispatches
/// <see cref="DeleteDnsRecordCommand"/> rather than duplicating it.
/// </remarks>
/// <param name="ownership">The rule that says a client may only touch their own domains.</param>
/// <param name="bus">Wolverine bus, used to reach the shared use case once ownership is settled.</param>
public sealed class DeleteMyDomainDnsRecordHandler(IDomainOwnership ownership, IMessageBus bus)
{
    /// <summary>Handles <see cref="DeleteMyDomainDnsRecordCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this acts on the caller's own domain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the record has been removed.</returns>
    /// <exception cref="DomainNotFoundException">
    /// Thrown when the domain is not the caller's, when no such domain exists, and when the caller
    /// has no client record -- all with the same wording and status.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task HandleAsync(DeleteMyDomainDnsRecordCommand cmd, CancellationToken ct)
    {
        await ownership.RequireOwnedByCallerAsync(cmd.DomainId, ct);
        await bus.InvokeAsync(new DeleteDnsRecordCommand(cmd.DomainId, cmd.RecordId), ct);
    }
}
