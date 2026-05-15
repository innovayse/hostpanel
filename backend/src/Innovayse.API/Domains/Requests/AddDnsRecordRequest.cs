namespace Innovayse.API.Domains.Requests;

using Innovayse.Domain.Domains;

/// <summary>Request payload for adding a new DNS record to a domain's zone.</summary>
public sealed class AddDnsRecordRequest
{
    /// <summary>Gets the record host or name (e.g. "@", "www", "mail").</summary>
    public required string Host { get; init; }

    /// <summary>Gets the DNS record type (A, AAAA, MX, CNAME, TXT, etc.).</summary>
    public required DnsRecordType Type { get; init; }

    /// <summary>Gets the record value (e.g. IP address, target hostname, TXT string).</summary>
    public required string Value { get; init; }

    /// <summary>Gets the time-to-live in seconds.</summary>
    public required int Ttl { get; init; }

    /// <summary>Gets the priority for MX/SRV records; null for other record types.</summary>
    public int? Priority { get; init; }
}
