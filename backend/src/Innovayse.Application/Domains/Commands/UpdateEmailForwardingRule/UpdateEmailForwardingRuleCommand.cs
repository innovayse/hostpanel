namespace Innovayse.Application.Domains.Commands.UpdateEmailForwardingRule;

/// <summary>Command to update an existing email forwarding rule on a domain.</summary>
/// <param name="DomainId">Primary key of the domain that owns the rule.</param>
/// <param name="RuleId">Primary key of the email forwarding rule to update.</param>
/// <param name="Source">New source alias or local part.</param>
/// <param name="Destination">New destination email address.</param>
/// <param name="IsActive">Whether the rule should be active.</param>
public record UpdateEmailForwardingRuleCommand(int DomainId, int RuleId, string Source, string Destination, bool IsActive);
