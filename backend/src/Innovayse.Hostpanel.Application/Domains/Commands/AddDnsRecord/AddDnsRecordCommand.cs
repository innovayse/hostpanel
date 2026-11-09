using Innovayse.Hostpanel.Domain.Domains.Enums;

namespace Innovayse.Hostpanel.Application.Domains.Commands.AddDnsRecord;

/// <summary>Command to add a new DNS record to a domain's zone.</summary>
/// <param name="DomainId">
/// Primary key of the domain to update. Nullable so the route id can overlay it: a non-nullable
/// member here would make the body required before <c>with { DomainId = id }</c> ever ran.
/// </param>
/// <param name="Type">DNS record type (A, AAAA, MX, CNAME, TXT, etc.).</param>
/// <param name="Host">Record host or name (e.g. "@", "www", "mail").</param>
/// <param name="Value">Record value (e.g. IP address, target hostname, TXT string).</param>
/// <param name="Ttl">Time-to-live in seconds.</param>
/// <param name="Priority">Priority for MX/SRV records; null for other types.</param>
public sealed record AddDnsRecordCommand(
    int? DomainId,
    DnsRecordType Type,
    string Host,
    string Value,
    int Ttl,
    int? Priority);
