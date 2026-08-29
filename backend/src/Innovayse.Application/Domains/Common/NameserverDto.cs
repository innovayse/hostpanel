namespace Innovayse.Application.Domains.Common;

/// <summary>DTO representing a name server assigned to a domain.</summary>
/// <param name="Id">Nameserver primary key.</param>
/// <param name="Host">Fully-qualified nameserver hostname (e.g. "ns1.example.com").</param>
public record NameserverDto(int Id, string Host);
