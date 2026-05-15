namespace Innovayse.Domain.Domains;

using Innovayse.Domain.Common;

/// <summary>
/// Represents a single DNS record belonging to a domain.
/// Owned by the <see cref="Domain"/> aggregate — cannot exist independently.
/// </summary>
public sealed class DnsRecord : Entity
{
    /// <summary>Gets the FK to the owning domain.</summary>
    public int DomainId { get; private set; }

    /// <summary>Gets the DNS record type (A, AAAA, MX, etc.).</summary>
    public DnsRecordType Type { get; private set; }

    /// <summary>Gets the DNS record host/name (e.g. "@", "www", "mail").</summary>
    public string Host { get; private set; } = string.Empty;

    /// <summary>Gets the DNS record value (e.g. IP address, target hostname, TXT string).</summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>Gets the time-to-live in seconds.</summary>
    public int Ttl { get; private set; }

    /// <summary>Gets the priority for MX/SRV records; null for other types.</summary>
    public int? Priority { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private DnsRecord() : base(0) { }

    /// <summary>
    /// Creates a new DNS record owned by the specified domain.
    /// </summary>
    /// <param name="domainId">FK to the owning <see cref="Domain"/>.</param>
    /// <param name="type">DNS record type.</param>
    /// <param name="host">Record host or name.</param>
    /// <param name="value">Record value.</param>
    /// <param name="ttl">Time-to-live in seconds.</param>
    /// <param name="priority">Priority for MX/SRV records; null for others.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="host"/> or <paramref name="value"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="ttl"/> is negative.</exception>
    internal DnsRecord(int domainId, DnsRecordType type, string host, string value, int ttl, int? priority)
        : base(0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (ttl < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must be non-negative.");
        }

        DomainId = domainId;
        Type = type;
        Host = host;
        Value = value;
        Ttl = ttl;
        Priority = priority;
    }

    /// <summary>
    /// Creates a standalone <see cref="DnsRecord"/> instance not yet attached to a domain aggregate.
    /// Used by registrar providers to return parsed records from external API responses.
    /// </summary>
    /// <param name="type">DNS record type.</param>
    /// <param name="host">Record host or name.</param>
    /// <param name="value">Record value.</param>
    /// <param name="ttl">Time-to-live in seconds.</param>
    /// <param name="priority">Priority for MX/SRV records; null for others.</param>
    /// <returns>A new <see cref="DnsRecord"/> with <see cref="Entity.Id"/> set to zero and <see cref="DomainId"/> set to zero.</returns>
    public static DnsRecord CreateDetached(DnsRecordType type, string host, string value, int ttl, int? priority)
    {
        return new DnsRecord(0, type, host, value, ttl, priority);
    }

    /// <summary>
    /// Updates the mutable fields of this DNS record.
    /// </summary>
    /// <param name="value">New record value.</param>
    /// <param name="ttl">New time-to-live in seconds.</param>
    /// <param name="priority">New priority, or null to clear it.</param>
    internal void Update(string value, int ttl, int? priority)
    {
        Value = value;
        Ttl = ttl;
        Priority = priority;
    }
}
