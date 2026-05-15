namespace Innovayse.Application.Domains.Commands.AddDnsRecord;

using Innovayse.Domain.Domains;

/// <summary>Command to add a new DNS record to a domain's zone.</summary>
/// <param name="DomainId">Primary key of the domain to update.</param>
/// <param name="Type">DNS record type (A, AAAA, MX, CNAME, TXT, etc.).</param>
/// <param name="Host">Record host or name (e.g. "@", "www", "mail").</param>
/// <param name="Value">Record value (e.g. IP address, target hostname, TXT string).</param>
/// <param name="Ttl">Time-to-live in seconds.</param>
/// <param name="Priority">Priority for MX/SRV records; null for other types.</param>
public record AddDnsRecordCommand(
    int DomainId,
    DnsRecordType Type,
    string Host,
    string Value,
    int Ttl,
    int? Priority);
