namespace Innovayse.Application.Domains.Commands.RegisterDomain;

using Innovayse.Application.Common;
using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Registers a new domain with the configured registrar provider and persists the resulting aggregate.
/// Calls <see cref="IRegistrarProvider.RegisterAsync"/> first, then creates and activates the domain aggregate.
/// </summary>
public sealed class RegisterDomainHandler(
    IRegistrarProvider registrar,
    IDomainRepository repo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="RegisterDomainCommand"/>.
    /// </summary>
    /// <param name="cmd">The register domain command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created domain ID.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the registrar rejects the registration.</exception>
    public async Task<int> HandleAsync(RegisterDomainCommand cmd, CancellationToken ct)
    {
        var request = new RegisterDomainRequest(
            cmd.DomainName,
            cmd.Years,
            cmd.WhoisPrivacy,
            cmd.AutoRenew,
            cmd.Nameserver1,
            cmd.Nameserver2);

        var result = await registrar.RegisterAsync(request, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Registrar rejected registration of '{cmd.DomainName}': {result.ErrorMessage}");
        }

        var expiresAt = result.ExpiresAt
            ?? DateTimeOffset.UtcNow.AddYears(cmd.Years);

        var domain = Domain.Register(cmd.ClientId, cmd.DomainName, expiresAt, cmd.AutoRenew, cmd.WhoisPrivacy);
        domain.Activate(result.RegistrarRef!);

        repo.Add(domain);
        await uow.SaveChangesAsync(ct);
        return domain.Id;
    }
}
