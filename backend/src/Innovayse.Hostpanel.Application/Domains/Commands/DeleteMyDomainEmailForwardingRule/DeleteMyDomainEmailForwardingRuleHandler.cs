using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.DeleteEmailForwardingRule;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Domains.Commands.DeleteMyDomainEmailForwardingRule;

/// <summary>
/// Deletes an email forwarding rule from one of the calling client's own domains, refusing every
/// domain that is not theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// drop somebody's mail rule through <see cref="DeleteMyDomainEmailForwardingRuleCommand"/> without
/// it having run. Once ownership is settled the work is the same the admin route performs, so this
/// dispatches <see cref="DeleteEmailForwardingRuleCommand"/> rather than duplicating it.
/// </remarks>
public sealed class DeleteMyDomainEmailForwardingRuleHandler
{
    /// <summary>The rule that says a client may only touch their own domains.</summary>
    private readonly IDomainOwnership _ownership;

    /// <summary>Wolverine bus, used to reach the shared use case once ownership is settled.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes a new instance of <see cref="DeleteMyDomainEmailForwardingRuleHandler"/>.</summary>
    /// <param name="ownership">The rule that says a client may only touch their own domains.</param>
    /// <param name="bus">Wolverine bus, used to reach the shared use case once ownership is settled.</param>
    public DeleteMyDomainEmailForwardingRuleHandler(IDomainOwnership ownership, IMessageBus bus)
    {
        _ownership = ownership;
        _bus = bus;
    }

    /// <summary>Handles <see cref="DeleteMyDomainEmailForwardingRuleCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this acts on the caller's own domain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the rule has been removed, or <c>DOMAIN_NOT_FOUND</c> when the domain is not
    /// the caller's, when no such domain exists, or when the caller has no client record, or
    /// <c>EMAIL_FORWARDING_RULE_NOT_FOUND</c> when the rule id names no rule on that domain, or
    /// <c>EMAIL_FORWARDING_RULE_UPSTREAM_FAILED</c> when the registrar rejects the deletion.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result> HandleAsync(DeleteMyDomainEmailForwardingRuleCommand cmd, CancellationToken ct)
    {
        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(cmd.DomainId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return ownershipOutcome;
        }

        return await _bus.InvokeAsync<Result>(
            new DeleteEmailForwardingRuleCommand(cmd.DomainId, cmd.RuleId), ct);
    }
}
