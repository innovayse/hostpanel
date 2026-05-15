namespace Innovayse.Domain.Domains;

/// <summary>
/// WHOIS data returned by a registrar lookup.
/// </summary>
/// <param name="Registrar">Name of the registrar holding the domain.</param>
/// <param name="Registrant">Name or organisation of the domain registrant.</param>
/// <param name="CreatedAt">UTC date the domain was first registered.</param>
/// <param name="ExpiresAt">UTC date the domain registration expires.</param>
public record WhoisInfo(string Registrar, string Registrant, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);
