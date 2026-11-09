using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.UpdateDnsRecord;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Domains.Commands.UpdateMyDomainDnsRecord;

/// <summary>
/// Updates a DNS record on one of the calling client's own domains, refusing every domain that is
/// not theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// rewrite a zone entry through <see cref="UpdateMyDomainDnsRecordCommand"/> without it having run.
/// Once ownership is settled the work is the same the admin route performs, so this dispatches
/// <see cref="UpdateDnsRecordCommand"/> rather than duplicating it.
/// </remarks>
public sealed class UpdateMyDomainDnsRecordHandler
{
    /// <summary>The rule that says a client may only touch their own domains.</summary>
    private readonly IDomainOwnership _ownership;

    /// <summary>Wolverine bus, used to reach the shared use case once ownership is settled.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes a new instance of <see cref="UpdateMyDomainDnsRecordHandler"/>.</summary>
    /// <param name="ownership">The rule that says a client may only touch their own domains.</param>
    /// <param name="bus">Wolverine bus, used to reach the shared use case once ownership is settled.</param>
    public UpdateMyDomainDnsRecordHandler(IDomainOwnership ownership, IMessageBus bus)
    {
        _ownership = ownership;
        _bus = bus;
    }

    /// <summary>Handles <see cref="UpdateMyDomainDnsRecordCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this acts on the caller's own domain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the record has been updated, or <c>DOMAIN_NOT_FOUND</c> when the domain is
    /// not the caller's, when no such domain exists, or when the caller has no client record, or
    /// <c>DNS_RECORD_NOT_FOUND</c> when the record id names no record on that domain, or
    /// <c>DNS_RECORD_UPSTREAM_FAILED</c> when the registrar rejects the update.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result> HandleAsync(UpdateMyDomainDnsRecordCommand cmd, CancellationToken ct)
    {
        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(cmd.DomainId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return ownershipOutcome;
        }

        return await _bus.InvokeAsync<Result>(
            new UpdateDnsRecordCommand(cmd.DomainId, cmd.RecordId, cmd.Value, cmd.Ttl, cmd.Priority),
            ct);
    }
}
