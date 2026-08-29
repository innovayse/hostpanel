namespace Innovayse.Application.Domains.Commands.ModifyMyDomainContact;

using Innovayse.Application.Domains.Commands.ModifyDomainContact;
using Innovayse.Application.Domains.Common;
using Wolverine;

/// <summary>
/// Modifies the registrant contact on one of the calling client's own domains, refusing every
/// domain that is not theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// rewrite a registrant contact through <see cref="ModifyMyDomainContactCommand"/> without it
/// having run. Once ownership is settled the work is the same the admin route performs, so this
/// dispatches <see cref="ModifyDomainContactCommand"/> rather than duplicating it.
/// </remarks>
/// <param name="ownership">The rule that says a client may only touch their own domains.</param>
/// <param name="bus">Wolverine bus, used to reach the shared use case once ownership is settled.</param>
public sealed class ModifyMyDomainContactHandler(IDomainOwnership ownership, IMessageBus bus)
{
    /// <summary>Handles <see cref="ModifyMyDomainContactCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this acts on the caller's own domain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the registrar has accepted the change.</returns>
    /// <exception cref="DomainNotFoundException">
    /// Thrown when the domain is not the caller's, when no such domain exists, and when the caller
    /// has no client record -- all with the same wording and status.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task HandleAsync(ModifyMyDomainContactCommand cmd, CancellationToken ct)
    {
        await ownership.RequireOwnedByCallerAsync(cmd.DomainId, ct);
        await bus.InvokeAsync(new ModifyDomainContactCommand(cmd.DomainId, cmd.Contact), ct);
    }
}
