namespace Innovayse.Application.Domains.DTOs;

/// <summary>DTO representing WHOIS information for a domain.</summary>
/// <param name="Registrar">Name of the domain registrar.</param>
/// <param name="Registrant">Name or identifier of the domain registrant.</param>
/// <param name="CreatedAt">Domain creation date (UTC).</param>
/// <param name="ExpiresAt">Domain expiration date (UTC).</param>
public record WhoisDto(string Registrar, string Registrant, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);
