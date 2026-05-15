namespace Innovayse.Application.Domains.Commands.InitiateOutgoingTransfer;

using Innovayse.Application.Common;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Initiates an outgoing domain transfer at the registrar, retrieves the EPP code,
/// and stores it on the domain aggregate.
/// </summary>
public sealed class InitiateOutgoingTransferHandler(
    IRegistrarProvider registrar,
    IDomainRepository repo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="InitiateOutgoingTransferCommand"/>.
    /// </summary>
    /// <param name="cmd">The initiate outgoing transfer command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The EPP (authorization) code to hand to the gaining registrar.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain is not found, the registrar rejects the request,
    /// or the EPP code cannot be retrieved.
    /// </exception>
    public async Task<string> HandleAsync(InitiateOutgoingTransferCommand cmd, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(cmd.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain {cmd.DomainId} not found.");

        if (domain.RegistrarRef is null)
        {
            throw new InvalidOperationException(
                $"Domain '{domain.Name}' has no registrar reference — cannot initiate outgoing transfer.");
        }

        var result = await registrar.InitiateOutgoingTransferAsync(domain.Name, domain.RegistrarRef, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Registrar rejected outgoing transfer initiation for '{domain.Name}': {result.ErrorMessage}");
        }

        var eppCode = await registrar.GetEppCodeAsync(domain.Name, domain.RegistrarRef, ct)
            ?? throw new InvalidOperationException(
                $"Registrar did not return an EPP code for domain '{domain.Name}'.");

        domain.SetEppCode(eppCode);
        await uow.SaveChangesAsync(ct);
        return eppCode;
    }
}
