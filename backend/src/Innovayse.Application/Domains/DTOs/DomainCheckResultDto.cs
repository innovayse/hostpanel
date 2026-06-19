namespace Innovayse.Application.Domains.DTOs;

/// <summary>Result of a domain availability check returned to the client.</summary>
/// <param name="Domain">The fully-qualified domain name that was checked (e.g. "example.com").</param>
/// <param name="Available">Whether the domain is available for registration.</param>
/// <param name="Status">Human-readable status string: "available" or "taken".</param>
public record DomainCheckResultDto(string Domain, bool Available, string Status);
