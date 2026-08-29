namespace Innovayse.Application.Domains.Commands.UpdateMyDomainEmailForwardingRule;

/// <summary>Command for a client to update an email forwarding rule on one of their own domains.</summary>
/// <remarks>
/// Carries a domain id but no client id. Which account the domain must belong to is resolved inside
/// the handler from the credential, so the scoping cannot be separated from the message the way an
/// id in the body can. The admin route that may act on any client's domain dispatches
/// <c>UpdateEmailForwardingRuleCommand</c> directly.
/// </remarks>
/// <param name="DomainId">Primary key of the domain, which must belong to the caller.</param>
/// <param name="RuleId">Primary key of the email forwarding rule to update.</param>
/// <param name="Source">New source alias or local part.</param>
/// <param name="Destination">New destination email address.</param>
/// <param name="IsActive">Whether the rule should be active.</param>
public sealed record UpdateMyDomainEmailForwardingRuleCommand(
    int DomainId,
    int RuleId,
    string Source,
    string Destination,
    bool IsActive);
