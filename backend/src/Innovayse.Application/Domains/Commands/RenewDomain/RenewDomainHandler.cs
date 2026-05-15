namespace Innovayse.Application.Domains.Commands.RenewDomain;

using Innovayse.Application.Common;
using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Renews an existing domain registration at the registrar and updates the aggregate's expiry date.
/// </summary>
public sealed class RenewDomainHandler(
    IRegistrarProvider registrar,
    IDomainRepository repo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="RenewDomainCommand"/>.
    /// </summary>
    /// <param name="cmd">The renew domain command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain is not found or the registrar rejects the renewal.
    /// </exception>
    public async Task HandleAsync(RenewDomainCommand cmd, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(cmd.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain {cmd.DomainId} not found.");

        DateTimeOffset newExpiresAt;

        if (domain.RegistrarRef is not null)
        {
            var request = new RenewDomainRequest(domain.Name, domain.RegistrarRef, cmd.Years);
            var result = await registrar.RenewAsync(request, ct);

            if (!result.Success)
            {
                throw new InvalidOperationException(
                    $"Registrar rejected renewal of '{domain.Name}': {result.ErrorMessage}");
            }

            newExpiresAt = result.ExpiresAt ?? domain.ExpiresAt.AddYears(cmd.Years);
        }
        else
        {
            newExpiresAt = domain.ExpiresAt.AddYears(cmd.Years);
        }

        domain.Renew(newExpiresAt);
        await uow.SaveChangesAsync(ct);
    }
}
