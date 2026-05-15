namespace Innovayse.Application.Domains.Queries.GetWhois;

/// <summary>Query to perform a WHOIS lookup for a domain name.</summary>
/// <param name="DomainName">The fully-qualified domain name to look up (e.g. "example.com").</param>
public record GetWhoisQuery(string DomainName);
