namespace Innovayse.Application.Domains.DTOs;

using Innovayse.Domain.Domains;

/// <summary>DTO representing a single DNS record belonging to a domain.</summary>
/// <param name="Id">DNS record primary key.</param>
/// <param name="Type">DNS record type (A, AAAA, MX, etc.).</param>
/// <param name="Host">DNS record host or name (e.g. "@", "www", "mail").</param>
/// <param name="Value">DNS record value (e.g. IP address, target hostname, TXT string).</param>
/// <param name="Ttl">Time-to-live in seconds.</param>
/// <param name="Priority">Priority for MX/SRV records; null for other types.</param>
public record DnsRecordDto(int Id, DnsRecordType Type, string Host, string Value, int Ttl, int? Priority);
