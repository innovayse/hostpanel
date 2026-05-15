namespace Innovayse.Application.Domains.Commands.TransferDomain;

using Innovayse.Application.Common;
using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Initiates an incoming domain transfer via the registrar provider and persists the resulting aggregate.
/// Creates the domain in <c>PendingTransfer</c> status, then transitions it to <c>Active</c> once the
/// registrar confirms.
/// </summary>
public sealed class TransferDomainHandler(
    IRegistrarProvider registrar,
    IDomainRepository repo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="TransferDomainCommand"/>.
    /// </summary>
    /// <param name="cmd">The transfer domain command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created domain ID.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the registrar rejects the transfer request.</exception>
    public async Task<int> HandleAsync(TransferDomainCommand cmd, CancellationToken ct)
    {
        var request = new TransferDomainRequest(cmd.DomainName, cmd.EppCode, cmd.WhoisPrivacy);
        var result = await registrar.TransferAsync(request, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Registrar rejected transfer of '{cmd.DomainName}': {result.ErrorMessage}");
        }

        var domain = Domain.CreateTransfer(cmd.ClientId, cmd.DomainName);

        var expiresAt = result.ExpiresAt ?? DateTimeOffset.UtcNow.AddYears(1);
        domain.ActivateTransfer(result.RegistrarRef!, expiresAt);

        repo.Add(domain);
        await uow.SaveChangesAsync(ct);
        return domain.Id;
    }
}
