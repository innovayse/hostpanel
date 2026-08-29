namespace Innovayse.Application.Domains.Commands.UpdateMyDomainEmailForwardingRule;

using Innovayse.Application.Domains.Commands.UpdateEmailForwardingRule;
using Innovayse.Application.Domains.Common;
using Wolverine;

/// <summary>
/// Updates an email forwarding rule on one of the calling client's own domains, refusing every
/// domain that is not theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// repoint somebody's mail through <see cref="UpdateMyDomainEmailForwardingRuleCommand"/> without
/// it having run. Once ownership is settled the work is the same the admin route performs, so this
/// dispatches <see cref="UpdateEmailForwardingRuleCommand"/> rather than duplicating it.
/// </remarks>
/// <param name="ownership">The rule that says a client may only touch their own domains.</param>
/// <param name="bus">Wolverine bus, used to reach the shared use case once ownership is settled.</param>
public sealed class UpdateMyDomainEmailForwardingRuleHandler(IDomainOwnership ownership, IMessageBus bus)
{
    /// <summary>Handles <see cref="UpdateMyDomainEmailForwardingRuleCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this acts on the caller's own domain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the rule has been updated.</returns>
    /// <exception cref="DomainNotFoundException">
    /// Thrown when the domain is not the caller's, when no such domain exists, and when the caller
    /// has no client record -- all with the same wording and status.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task HandleAsync(UpdateMyDomainEmailForwardingRuleCommand cmd, CancellationToken ct)
    {
        await ownership.RequireOwnedByCallerAsync(cmd.DomainId, ct);
        await bus.InvokeAsync(
            new UpdateEmailForwardingRuleCommand(cmd.DomainId, cmd.RuleId, cmd.Source, cmd.Destination, cmd.IsActive),
            ct);
    }
}
