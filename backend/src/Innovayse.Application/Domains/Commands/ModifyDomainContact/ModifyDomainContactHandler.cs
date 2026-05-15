namespace Innovayse.Application.Domains.Commands.ModifyDomainContact;

using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Sends updated WHOIS registrant contact details to the registrar for a domain.
/// </summary>
public sealed class ModifyDomainContactHandler(
    IDomainRepository repo,
    IRegistrarProvider registrar)
{
    /// <summary>
    /// Handles <see cref="ModifyDomainContactCommand"/>.
    /// </summary>
    /// <param name="cmd">The modify domain contact command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the domain is not found or the registrar rejects the contact update.
    /// </exception>
    public async Task HandleAsync(ModifyDomainContactCommand cmd, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(cmd.DomainId, ct)
            ?? throw new InvalidOperationException($"Domain {cmd.DomainId} not found.");

        var result = await registrar.ModifyContactDetailsAsync(domain.Name, cmd.Contact, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Registrar failed to modify contact details for '{domain.Name}': {result.ErrorMessage}");
        }
    }
}
