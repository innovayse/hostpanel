namespace Innovayse.Application.Domains.Commands.AddMyDomainDnsRecord;

using Innovayse.Domain.Domains;

/// <summary>Command for a client to add a DNS record to one of their own domains.</summary>
/// <remarks>
/// Carries a domain id but no client id. Which account the domain must belong to is resolved inside
/// the handler from the credential, so the scoping cannot be separated from the message the way an
/// id in the body can. The admin route that may act on any client's domain dispatches
/// <c>AddDnsRecordCommand</c> directly.
/// </remarks>
/// <param name="DomainId">Primary key of the domain, which must belong to the caller.</param>
/// <param name="Type">DNS record type (A, AAAA, MX, CNAME, TXT, etc.).</param>
/// <param name="Host">Record host or name (e.g. "@", "www", "mail").</param>
/// <param name="Value">Record value (e.g. IP address, target hostname, TXT string).</param>
/// <param name="Ttl">Time-to-live in seconds.</param>
/// <param name="Priority">Priority for MX/SRV records; null for other types.</param>
public sealed record AddMyDomainDnsRecordCommand(
    int DomainId,
    DnsRecordType Type,
    string Host,
    string Value,
    int Ttl,
    int? Priority);
