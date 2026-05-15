namespace Innovayse.Application.Domains.Commands.SetWhoisPrivacy;

using Innovayse.Application.Common;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Enables or disables WHOIS privacy for a domain at both the registrar and aggregate levels.
/// </summary>
public sealed class SetWhoisPrivacyHandler(
    IRegistrarProvider registrar,
    IDomainRepository repo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="SetWhoisPrivacyCommand"/>.
    /// </summary>
    /// <param name="cmd">The set WHOIS privacy command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain is not found or the registrar rejects the update.
    /// </exception>
    public async Task HandleAsync(SetWhoisPrivacyCommand cmd, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(cmd.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain {cmd.DomainId} not found.");

        if (domain.RegistrarRef is not null)
        {
            var result = await registrar.SetWhoisPrivacyAsync(domain.Name, domain.RegistrarRef, cmd.Value, ct);

            if (!result.Success)
            {
                throw new InvalidOperationException(
                    $"Registrar failed to update WHOIS privacy for '{domain.Name}': {result.ErrorMessage}");
            }
        }

        domain.SetWhoisPrivacy(cmd.Value);
        await uow.SaveChangesAsync(ct);
    }
}
