namespace Innovayse.Application.Domains.Queries.CheckDomainAvailability;

/// <summary>Query to check whether a domain name is available for registration.</summary>
/// <param name="DomainName">The fully-qualified domain name to check (e.g. "example.com").</param>
public record CheckDomainAvailabilityQuery(string DomainName);
