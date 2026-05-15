namespace Innovayse.Application.Domains.Commands.AddEmailForwardingRule;

/// <summary>Command to add an email forwarding rule to a domain.</summary>
/// <param name="DomainId">Primary key of the domain to add the rule to.</param>
/// <param name="Source">Source alias or local part (e.g. "info").</param>
/// <param name="Destination">Destination email address.</param>
public record AddEmailForwardingRuleCommand(int DomainId, string Source, string Destination);
