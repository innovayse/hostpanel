namespace Innovayse.Application.Domains.Commands.DeleteEmailForwardingRule;

/// <summary>Command to delete an email forwarding rule from a domain.</summary>
/// <param name="DomainId">Primary key of the domain that owns the rule.</param>
/// <param name="RuleId">Primary key of the email forwarding rule to delete.</param>
public record DeleteEmailForwardingRuleCommand(int DomainId, int RuleId);
