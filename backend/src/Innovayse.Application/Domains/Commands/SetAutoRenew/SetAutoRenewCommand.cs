namespace Innovayse.Application.Domains.Commands.SetAutoRenew;

/// <summary>Command to enable or disable automatic renewal for a domain.</summary>
/// <param name="DomainId">Primary key of the domain to update.</param>
/// <param name="Value">The desired auto-renew flag value.</param>
public record SetAutoRenewCommand(int DomainId, bool Value);
