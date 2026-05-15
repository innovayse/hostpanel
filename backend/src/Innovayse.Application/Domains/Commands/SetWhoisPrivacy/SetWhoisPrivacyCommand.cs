namespace Innovayse.Application.Domains.Commands.SetWhoisPrivacy;

/// <summary>Command to enable or disable WHOIS privacy for a domain.</summary>
/// <param name="DomainId">Primary key of the domain to update.</param>
/// <param name="Value">The desired WHOIS privacy flag value.</param>
public record SetWhoisPrivacyCommand(int DomainId, bool Value);
