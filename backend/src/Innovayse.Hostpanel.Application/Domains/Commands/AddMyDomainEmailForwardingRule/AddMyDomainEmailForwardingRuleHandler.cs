using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.AddEmailForwardingRule;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Domains.Commands.AddMyDomainEmailForwardingRule;

/// <summary>
/// Adds an email forwarding rule to one of the calling client's own domains, refusing every domain
/// that is not theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// redirect somebody's mail through <see cref="AddMyDomainEmailForwardingRuleCommand"/> without it
/// having run. Once ownership is settled the work is the same the admin route performs, so this
/// dispatches <see cref="AddEmailForwardingRuleCommand"/> rather than duplicating it.
/// </remarks>
public sealed class AddMyDomainEmailForwardingRuleHandler
{
    /// <summary>The rule that says a client may only touch their own domains.</summary>
    private readonly IDomainOwnership _ownership;

    /// <summary>Wolverine bus, used to reach the shared use case once ownership is settled.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes a new instance of <see cref="AddMyDomainEmailForwardingRuleHandler"/>.</summary>
    /// <param name="ownership">The rule that says a client may only touch their own domains.</param>
    /// <param name="bus">Wolverine bus, used to reach the shared use case once ownership is settled.</param>
    public AddMyDomainEmailForwardingRuleHandler(IDomainOwnership ownership, IMessageBus bus)
    {
        _ownership = ownership;
        _bus = bus;
    }

    /// <summary>Handles <see cref="AddMyDomainEmailForwardingRuleCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this acts on the caller's own domain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the rule has been added, or <c>DOMAIN_NOT_FOUND</c> when the domain is not the
    /// caller's, when no such domain exists, or when the caller has no client record, or
    /// <c>EMAIL_FORWARDING_RULE_UPSTREAM_FAILED</c> when the registrar rejects the addition.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result> HandleAsync(AddMyDomainEmailForwardingRuleCommand cmd, CancellationToken ct)
    {
        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(cmd.DomainId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return ownershipOutcome;
        }

        return await _bus.InvokeAsync<Result>(
            new AddEmailForwardingRuleCommand(cmd.DomainId, cmd.Source, cmd.Destination), ct);
    }
}
