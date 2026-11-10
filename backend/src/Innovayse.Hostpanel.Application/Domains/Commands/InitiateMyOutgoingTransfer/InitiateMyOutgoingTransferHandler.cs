using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.InitiateOutgoingTransfer;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Domains.Commands.InitiateMyOutgoingTransfer;

/// <summary>
/// Starts an outgoing transfer of one of the calling client's own domains, refusing every domain
/// that is not theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// obtain an EPP authorization code through <see cref="InitiateMyOutgoingTransferCommand"/> without
/// it having run. Once ownership is settled the work is the same the admin route performs, so this
/// dispatches <see cref="InitiateOutgoingTransferCommand"/> rather than duplicating it.
/// </remarks>
public sealed class InitiateMyOutgoingTransferHandler
{
    /// <summary>The rule that says a client may only touch their own domains.</summary>
    private readonly IDomainOwnership _ownership;

    /// <summary>Wolverine bus, used to reach the shared use case once ownership is settled.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes a new instance of <see cref="InitiateMyOutgoingTransferHandler"/>.</summary>
    /// <param name="ownership">The rule that says a client may only touch their own domains.</param>
    /// <param name="bus">Wolverine bus, used to reach the shared use case once ownership is settled.</param>
    public InitiateMyOutgoingTransferHandler(IDomainOwnership ownership, IMessageBus bus)
    {
        _ownership = ownership;
        _bus = bus;
    }

    /// <summary>Handles <see cref="InitiateMyOutgoingTransferCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this acts on the caller's own domain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The EPP authorization code, as an <see cref="EppCodeDto"/>, to hand to the gaining registrar, or <c>DOMAIN_NOT_FOUND</c>
    /// when the domain is not the caller's, when no such domain exists, or when the caller has no
    /// client record, or <c>DOMAIN_NO_REGISTRAR_REF</c> when the domain carries no registrar
    /// reference, or <c>DOMAIN_OUTGOING_TRANSFER_UPSTREAM_FAILED</c> when the registrar rejects the
    /// request, or <c>DOMAIN_EPP_CODE_UPSTREAM_FAILED</c> when the registrar returns no code.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result<EppCodeDto>> HandleAsync(InitiateMyOutgoingTransferCommand cmd, CancellationToken ct)
    {
        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(cmd.DomainId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return Result<EppCodeDto>.FailureCode(ownershipOutcome.ErrorCode!);
        }

        return await _bus.InvokeAsync<Result<EppCodeDto>>(new InitiateOutgoingTransferCommand(cmd.DomainId), ct);
    }
}
