namespace Innovayse.Application.Domains.Commands.RegisterDomain;

/// <summary>Command to register a new domain name for a client.</summary>
/// <param name="ClientId">The client requesting domain registration.</param>
/// <param name="DomainName">Fully-qualified domain name to register (e.g. "example.com").</param>
/// <param name="Years">Number of years to register the domain for (1–10).</param>
/// <param name="WhoisPrivacy">Whether to enable WHOIS privacy on registration.</param>
/// <param name="AutoRenew">Whether to enable automatic renewal at the registrar.</param>
/// <param name="Nameserver1">Primary nameserver hostname; null to use registrar defaults.</param>
/// <param name="Nameserver2">Secondary nameserver hostname; null to use registrar defaults.</param>
public record RegisterDomainCommand(
    int ClientId,
    string DomainName,
    int Years,
    bool WhoisPrivacy,
    bool AutoRenew,
    string? Nameserver1,
    string? Nameserver2);
