using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.SetWhoisPrivacy;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Domains.Commands.SetMyDomainWhoisPrivacy;

/// <summary>
/// Sets WHOIS privacy on one of the calling client's own domains, refusing every domain that is not
/// theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// change a privacy setting through <see cref="SetMyDomainWhoisPrivacyCommand"/> without it having
/// run. Once ownership is settled the work is the same the admin route performs, so this dispatches
/// <see cref="SetWhoisPrivacyCommand"/> rather than duplicating it.
/// </remarks>
public sealed class SetMyDomainWhoisPrivacyHandler
{
    /// <summary>The rule that says a client may only touch their own domains.</summary>
    private readonly IDomainOwnership _ownership;

    /// <summary>Wolverine bus, used to reach the shared use case once ownership is settled.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes a new instance of <see cref="SetMyDomainWhoisPrivacyHandler"/>.</summary>
    /// <param name="ownership">The rule that says a client may only touch their own domains.</param>
    /// <param name="bus">Wolverine bus, used to reach the shared use case once ownership is settled.</param>
    public SetMyDomainWhoisPrivacyHandler(IDomainOwnership ownership, IMessageBus bus)
    {
        _ownership = ownership;
        _bus = bus;
    }

    /// <summary>Handles <see cref="SetMyDomainWhoisPrivacyCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this acts on the caller's own domain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the setting has been applied, or <c>DOMAIN_NOT_FOUND</c> when the domain is
    /// not the caller's, when no such domain exists, or when the caller has no client record, or
    /// <c>WHOIS_PRIVACY_UPSTREAM_FAILED</c> when the registrar rejects the update.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result> HandleAsync(SetMyDomainWhoisPrivacyCommand cmd, CancellationToken ct)
    {
        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(cmd.DomainId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return ownershipOutcome;
        }

        return await _bus.InvokeAsync<Result>(new SetWhoisPrivacyCommand(cmd.DomainId, cmd.Value!.Value), ct);
    }
}
