namespace Innovayse.Application.Domains.Commands.UpdateNameservers;

/// <summary>Command to replace the nameserver list for a domain.</summary>
/// <param name="DomainId">Primary key of the domain to update.</param>
/// <param name="Nameservers">New ordered list of nameserver hostnames (minimum 2).</param>
public record UpdateNameserversCommand(int DomainId, IReadOnlyList<string> Nameservers);
