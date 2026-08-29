namespace Innovayse.Application.Domains.Commands.DeleteMyDomainEmailForwardingRule;

/// <summary>Command for a client to delete an email forwarding rule from one of their own domains.</summary>
/// <remarks>
/// Carries a domain id but no client id. Which account the domain must belong to is resolved inside
/// the handler from the credential, so the scoping cannot be separated from the message the way an
/// id in the body can. The admin route that may act on any client's domain dispatches
/// <c>DeleteEmailForwardingRuleCommand</c> directly.
/// </remarks>
/// <param name="DomainId">Primary key of the domain, which must belong to the caller.</param>
/// <param name="RuleId">Primary key of the email forwarding rule to delete.</param>
public sealed record DeleteMyDomainEmailForwardingRuleCommand(
    int DomainId,
    int RuleId);
